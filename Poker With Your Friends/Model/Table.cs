using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlRoot("Table")]
    public partial class Table : ObservableObject
    {
        public enum PlayerAction
        {
            Call,
            Raise,
            Fold
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
            Round++;
            TableText = "Round started with players: //";
            Pot = 0;
            Housecards.Clear();
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
            TableText = $"In game, active player: {player.Name}";
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
                    player.Call();
                    break;
                case PlayerAction.Raise:
                    player.Raise(decision.Amount);
                    break;
                case PlayerAction.Fold:
                    player.Fold();
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

        async Task Play()
        {
            try
            {
                while (true)
                {
                    TableText = "Table Inactive, waiting for 2 players to join";
                    await EnoughplayersJoined.WaitAsync();
                    lock (_playerJoinLock)
                    {
                        _enoughPlayersSignaled = false;
                    }
                    TableText = "Enough players joined Starting soon!";

                    if (!await WaitForStartDelayAsync())
                    {
                        continue;
                    }

                    break;
                }

                IsGameActive = true;
                StartRound();
                DealToPlayers();

                Housecards.Add(deck.DrawCard());
                Housecards.Add(deck.DrawCard());
                Housecards.Add(deck.DrawCard());

                OnUpdateTableRequest?.Invoke(this);

                foreach (var player in Players.ToList())
                {
                    if (!Players.Contains(player)) continue;

                    await WaitForPlayerActionAsync(player);
                    OnUpdateTableRequest?.Invoke(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Play() crashed: {ex}");
                IsGameActive = false;
                OnUpdateTableRequest?.Invoke(this);
            }
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
    }
}
