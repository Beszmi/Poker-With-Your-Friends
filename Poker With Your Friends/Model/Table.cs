using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model;

[XmlRoot("Table")]
public partial class Table : ObservableObject
{
    public enum PlayerAction
    {
        Call,
        Raise,
        Fold,
        AllIn
    }

    public readonly struct PlayerDecision
    {
        public PlayerAction Action { get; }
        public int Amount { get; }

        public PlayerDecision(PlayerAction action, int amount = 0)
        {
            Action = action;
            Amount = amount;
        }
    }

    /* -------------------------------------------------
     *  Properties
     *
     -------------------------------------------------*/
    [XmlIgnore]
    private readonly SemaphoreSlim EnoughplayersJoined = new SemaphoreSlim(0, 1);
    [XmlIgnore]
    private bool _enoughPlayersSignaled = false;
    [XmlIgnore]
    private readonly object _playerJoinLock = new object();

    [XmlIgnore]
    private Deck deck = new Deck();

    [XmlIgnore]
    [ObservableProperty]
    private partial Player? CurrentlyActivePlayer { get; set; } = null;

    /*------------------------------
     * FOR NETWORK TRANSMISSION ONLY
    ------------------------------*/
    [XmlAttribute("ActivePlayerName")]
    [ObservableProperty]
    public partial String ActivePlayerName { get; set; } = string.Empty;

    [XmlIgnore]
    private static int maxPlayers = 6;

    [XmlIgnore]
    public static Action<Table>? OnUpdateTableRequest;

    [XmlIgnore]
    public static Action<Table>? OnUpdateTextRequest;

    [XmlAttribute("Name")]
    [ObservableProperty]
    public partial String Name { get; set; }

    [XmlAttribute("Round")]
    [ObservableProperty]
    public partial int Round { get; set; } = 0;

    [XmlAttribute("SmallBlind")]
    [ObservableProperty]
    public partial int SmallBlind { get; set; } = 5;

    [XmlAttribute("ToCall")]
    [ObservableProperty]
    public partial int ToCall { get; set; } = 0;

    [XmlAttribute("Antee")]
    [ObservableProperty]
    public partial int Antee { get; set; } = 5;

    [XmlArray("Housecards")]
    [XmlArrayItem("Housecard")]
    [ObservableProperty]
    public partial ObservableCollection<Card> Housecards { get; set; } = new ObservableCollection<Card>();

    [XmlAttribute("Pot")]
    [ObservableProperty]
    public partial int Pot { get; set; }

    [XmlArray("Players")]
    [XmlArrayItem("Player")]
    [ObservableProperty]
    public partial ObservableCollection<Player> Players { get; set; } = new ObservableCollection<Player>();

    [XmlAttribute("IsGameActive")]
    [ObservableProperty]
    public partial bool IsGameActive { get; set; } = false;

    [XmlIgnore]
    public TaskCompletionSource<PlayerDecision>? PlayerActionTcs { get; private set; }

    [XmlIgnore]
    private readonly object _actionLock = new object();
    [XmlIgnore]
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(30);
    [XmlIgnore]
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(5);

    [XmlIgnore]
    public static Action<Table, int>? OnTimerStartRequest;

    [XmlIgnore]
    public static Action<String>? OnTableLogicError;

    [XmlAttribute("TableText")]
    [ObservableProperty]
    public partial String TableText { get; set; } = "Empty text";

    /* -------------------------------------------------
     *  Constructors
     *
     -------------------------------------------------*/
    public Table() { }

    public Table(String name)
    {
        Name = name;
    }

    /* -------------------------------------------------
     *  Methods
     *
     -------------------------------------------------*/
    public void AddPlayer(Player player)
    {
        if (!player.IsAtTable)
        {
            Players.Add(player);
            player.IsAtTable = true;
        }
        if (Players.Count > maxPlayers)
        {
            throw new InvalidOperationException("Table is full. Cannot add more players.");
        }

        lock (_playerJoinLock)
        {
            if (Players.Count >= 2 && !IsGameActive && !_enoughPlayersSignaled)
            {
                _enoughPlayersSignaled = true;
                EnoughplayersJoined.Release();
            }
        }
    }

    public void RemovePlayer(Player player)
    {
        if (player.IsAtTable)
        {
            Players.Remove(player);
            player.IsAtTable = false;
        }
    }

    public void StartRound()
    {
        BeforeBigBlind = true;
        Round++;
        SmallBlind = Round * 5;
        ToCall = SmallBlind;
        TableText = "Round started with players: //";
        Pot = 0;
        Housecards.Clear();
        foreach(Player player in Players)
        {
            player.ClearCards();
        }
        ZeroAllBets();
    }

    public void DealToPlayers()
    {
        foreach (var player in Players)
        {
            player.ClearCards();
            player.AddCard(deck.DrawCard());
        }
        foreach (var player in Players)
        {
            player.AddCard(deck.DrawCard());
        }
        OnUpdateTableRequest?.Invoke(this);
    }

    public async Task WaitForPlayerActionAsync(Player player)
    {
        if (player.HasFolded || !IsGameActive)
        {
            ActivePlayerName = string.Empty;
            return;
        }
        var tcs = new TaskCompletionSource<PlayerDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_actionLock)
        {
            PlayerActionTcs = tcs;
            CurrentlyActivePlayer = player;
        }
        int amountToCall = Math.Max(0, ToCall - player.RoundBet);
        TableTextUpdate($"Table active, currently waiting for player: {player.Name}, they covered this round: {player.RoundBet}, needs to pay in: {amountToCall}");
        ActivePlayerName = player.Name;
        player.IsCurrentlyActivePlayer = true;
        OnUpdateTableRequest?.Invoke(this);

        PlayerDecision decision;
        var timeoutTask = Task.Delay(ActionTimeout);
        OnTimerStartRequest?.Invoke(this, ActionTimeout.Seconds - 1); // 1 second less to compensate for any network lag or delay
        var finished = await Task.WhenAny(tcs.Task, timeoutTask);

        if (finished == timeoutTask)
        {
            System.Diagnostics.Debug.WriteLine($"{player.Name} timed out — auto-folding.");
            decision = new PlayerDecision(PlayerAction.Fold);
            tcs.TrySetResult(decision); // in case a late action races in right after
        }
        else
        {
            decision = await tcs.Task;
        }

        switch (decision.Action)
        {
            case PlayerAction.Call:
                int callAmount = ToCall - player.RoundBet;
                if (callAmount > 0)
                {
                    player.Spend(callAmount);
                    Pot += callAmount;
                }
                break;
            case PlayerAction.Raise:
                player.Spend(decision.Amount);
                Pot += decision.Amount;
                ToCall = player.RoundBet;
                ResetPlayersNeedToCover();
                break;
            case PlayerAction.Fold:
                player.Fold();
                break;
            case PlayerAction.AllIn:
                int allInAmount = Math.Min(decision.Amount, player.Chips);
                if (allInAmount <= 0 && player.Chips > 0)
                {
                    allInAmount = player.Chips;
                }
                player.Spend(allInAmount);
                Pot += allInAmount;
                if (player.RoundBet > ToCall)
                {
                    ToCall = player.RoundBet;
                    ResetPlayersNeedToCover();
                }
                player.IsAllIn = true;
                break;
        }

        player.IsCurrentlyActivePlayer = false;

        lock (_actionLock)
        {
            if (CurrentlyActivePlayer == player)
            {
                CurrentlyActivePlayer = null;
                PlayerActionTcs = null;
            }
        }

        ActivePlayerName = string.Empty;
        OnUpdateTableRequest?.Invoke(this);
    }

    async Task<bool> WaitForStartDelayAsync()
    {
        OnTimerStartRequest?.Invoke(this, StartDelay.Seconds - 1);
        var remaining = StartDelay;
        while (remaining > TimeSpan.Zero)
        {
            if (Players.Count < 2)
            {
                return false;
            }

            var wait = remaining > TimeSpan.FromMilliseconds(100)
                ? TimeSpan.FromMilliseconds(100)
                : remaining;
            await Task.Delay(wait);
            remaining -= wait;
        }

        return Players.Count >= 2;
    }

    ///-------------------------------------------------------------
    /// 
    ///     Main Game loop logic
    ///  
    ///-------------------------------------------------------------

    [XmlIgnore]
    private int PlayersNeedToCover = 0;

    [XmlIgnore]
    private bool BeforeBigBlind = false;

    async Task Play()
    {
        while (true)
        {
            try
            {
                await WaitForEnoughPlayersAsync();
                TableTextUpdate("Enough players joined Starting soon!");

                if (!await WaitForStartDelayAsync())
                {
                    continue;
                }

                if (Players.Count < 2)
                {
                    continue;
                }

                await PlaySingleHandAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Play() crashed: {ex}");
                OnTableLogicError?.Invoke($"Play() crashed: {ex}");
                IsGameActive = false;
                OnUpdateTableRequest?.Invoke(this);
            }
        }
    }

    private async Task WaitForEnoughPlayersAsync()
    {
        if (Players.Count >= 2)
        {
            return;
        }

        TableTextUpdate("Table Inactive, waiting for 2 players to join");
        await EnoughplayersJoined.WaitAsync();
        lock (_playerJoinLock)
        {
            _enoughPlayersSignaled = false;
        }
    }

    private async Task PlaySingleHandAsync()
    {
        IsGameActive = true;
        StartRound();

        bool handOver = await PlayRound();

        if (!handOver)
        {
            DealToPlayers();
            handOver = await PlayRound();
        }

        if (!handOver)
        {
            Housecards.Add(deck.DrawCard());
            Housecards.Add(deck.DrawCard());
            Housecards.Add(deck.DrawCard());
            handOver = await PlayRound();
        }

        if (!handOver)
        {
            Housecards.Add(deck.DrawCard());
            handOver = await PlayRound();
        }

        if (!handOver)
        {
            Housecards.Add(deck.DrawCard());
            await PlayRound();
        }

        EndGameLogic();
        ResetHandState();
    }

    private void ResetHandState()
    {
        IsGameActive = false;
        ActivePlayerName = string.Empty;
        CurrentlyActivePlayer = null;

        lock (_actionLock)
        {
            PlayerActionTcs?.TrySetCanceled();
            PlayerActionTcs = null;
        }

        foreach (var player in Players)
        {
            player.HasFolded = false;
            player.IsAllIn = false;
            player.IsCurrentlyActivePlayer = false;
            player.Hand = null;
        }

        BeforeBigBlind = true;
        TableTextUpdate("Hand complete. Next hand starting soon...");
        OnUpdateTableRequest?.Invoke(this);
    }

    private int CountPlayersWhoCanAct()
    {
        return Players.Count(p => !p.HasFolded && !p.IsAllIn);
    }

    private void ResetPlayersNeedToCover()
    {
        PlayersNeedToCover = CountPlayersWhoCanAct();
    }

    private bool IsHandOver() => Players.Count(p => !p.HasFolded) <= 1;

    private async Task<bool> PlayRound()
    {
        ResetPlayersNeedToCover();
        if (!BeforeBigBlind)
        {
            ToCall = 0;
        }

        if (PlayersNeedToCover <= 1)
        {
            ZeroRoundBets();
            OnUpdateTableRequest?.Invoke(this);
            return IsHandOver();
        }

        while (PlayersNeedToCover > 0)
        {
            foreach (var player in Players.ToList())
            {
                if (!Players.Contains(player)) continue;
                if (player.HasFolded || player.IsAllIn) continue;
                if (PlayersNeedToCover <= 0) break;
                
                await WaitForPlayerActionAsync(player);

                if (BeforeBigBlind)
                {
                    ToCall += 10;
                    BeforeBigBlind = false;
                }

                PlayersNeedToCover--;
                OnUpdateTableRequest?.Invoke(this);

                if (CountPlayersWhoCanAct() <= 1)
                {
                    PlayersNeedToCover = 0;
                    break;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"\nPlayersNeedToCover: {PlayersNeedToCover}\n");
        }

        ZeroRoundBets();
        OnUpdateTableRequest?.Invoke(this);
        return IsHandOver();
    }

    public bool SubmitPlayerAction(Player player, PlayerAction action, int amount = 0)
    {
        lock (_actionLock)
        {
            if (PlayerActionTcs == null || PlayerActionTcs.Task.IsCompleted)
            {
                return false;
            }
            if (CurrentlyActivePlayer == null ||
                !string.Equals(CurrentlyActivePlayer.Name, player.Name, StringComparison.Ordinal))
            {
                return false;
            }
            return PlayerActionTcs.TrySetResult(new PlayerDecision(action, amount));
        }
    }

    public void InitializeServerTable()
    {
        deck.Shuffle();
        Task.Run(() => Play());
    }

    public void HandlePlayerDisconnected(Player player)
    {
        if (!Players.Contains(player))
        {
            return;
        }
        player.HasFolded = true;

        lock (_actionLock)
        {
            if (ReferenceEquals(CurrentlyActivePlayer, player) &&
                PlayerActionTcs != null &&
                !PlayerActionTcs.Task.IsCompleted)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-folding for disconnected player: {player.Name}");
                PlayerActionTcs.TrySetResult(new PlayerDecision(PlayerAction.Fold));
            }
        }

        RemovePlayer(player);
        OnUpdateTableRequest?.Invoke(this);
    }

    public void ChangeActivePlayerByName(String name) //For clients
    {
        CurrentlyActivePlayer = string.IsNullOrEmpty(name)
            ? null
            : Game.ClientInstance.GetPlayerFromName(name);
    }

    public void TimerUp()
    {
        lock (_actionLock)
        {
            if (PlayerActionTcs == null || PlayerActionTcs.Task.IsCompleted)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"{CurrentlyActivePlayer?.Name ?? "Unknown player"} timed out — auto-folding.");
            PlayerActionTcs.TrySetResult(new PlayerDecision(PlayerAction.Fold));
        }
    }

    public static void HandleUpdateFromNetwork(int tableIndex, Table networkTable)
    {
        if (tableIndex < 0 || tableIndex >= Game.ClientInstance.Tables.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex), "Network update referenced an unknown table index.");
        }

        Table localTable = Game.ClientInstance.Tables[tableIndex];
        localTable.ActivePlayerName = networkTable.ActivePlayerName;
        localTable.ChangeActivePlayerByName(networkTable.ActivePlayerName);
        localTable.Name = networkTable.Name;
        localTable.Round = networkTable.Round;
        localTable.SmallBlind = networkTable.SmallBlind;
        localTable.Pot = networkTable.Pot;
        localTable.IsGameActive = networkTable.IsGameActive;
        localTable.TableText = networkTable.TableText;
        localTable.Antee = networkTable.Antee;
        localTable.ToCall = networkTable.ToCall;

        localTable.Housecards.Clear();
        foreach (Card card in networkTable.Housecards)
        {
            localTable.Housecards.Add(card);
        }

        foreach (Player seatedPlayer in localTable.Players)
        {
            seatedPlayer.IsAtTable = false;
        }

        localTable.Players.Clear();
        if (networkTable.Players.Count > maxPlayers)
        {
            throw new ArgumentException("Recieved Network table has too many players.");
        }

        foreach (Player networkPlayer in networkTable.Players)
        {
            Player localPlayer = Game.ClientInstance.GetPlayerFromName(networkPlayer.Name);
            localTable.Players.Add(localPlayer);
            localPlayer.UpdateProperties(networkPlayer);
            localPlayer.IsAtTable = true;
            if (localPlayer.Cards.Count == 2)
            {
                List<Card> handcards = new List<Card>();
                handcards.AddRange(localPlayer.Cards);
                handcards.AddRange(localTable.Housecards);
                localPlayer.Hand = new Hand(handcards.ToArray());
            }
            else
            {
                localPlayer.Hand = null;
            }
        }
    }

    public static int GetTableIdByName(String name)
    {
        var table = Game.ClientInstance.Tables.FirstOrDefault(t => t.Name == name);
        return Game.ClientInstance.Tables.IndexOf(table);
    }

    public void TableTextUpdate(String text)
    {
        TableText = text;
        OnUpdateTextRequest?.Invoke(this);
    }
    private void ZeroRoundBets()
    {
        foreach(Player player in Players)
        {
            player.RoundBet = 0;
        }
    }

    private void ZeroAllBets()
    {
        foreach (Player player in Players)
        {
            player.RoundBet = 0;
            player.PotBet = 0;
        }
    }

    private void EndGameLogic()
    {
        var remainingPlayers = Players.Where(p => !p.HasFolded).ToList();
        if (remainingPlayers.Count == 0 || Pot <= 0)
        {
            return;
        }

        int potToAward = Pot;
        Pot = 0;

        if (remainingPlayers.Count == 1)
        {
            remainingPlayers[0].Win(potToAward);
            OnUpdateTableRequest?.Invoke(this);
            return;
        }

        foreach (var player in remainingPlayers)
        {
            var handCards = player.Cards.Concat(Housecards).ToArray();
            player.Hand = handCards.Length >= 2 ? new Hand(handCards) : null;
        }

        var eligiblePlayers = remainingPlayers.Where(p => p.Hand != null).ToList();
        if (eligiblePlayers.Count == 0)
        {
            return;
        }

        var bestHand = eligiblePlayers
            .Select(p => p.Hand)
            .Max()!;

        var winners = eligiblePlayers
            .Where(p => p.Hand!.CompareTo(bestHand) == 0)
            .ToList();

        int share = potToAward / winners.Count;
        int remainder = potToAward % winners.Count;
        for (int i = 0; i < winners.Count; i++)
        {
            winners[i].Win(share + (i < remainder ? 1 : 0));
        }

        OnUpdateTableRequest?.Invoke(this);
    }
}
