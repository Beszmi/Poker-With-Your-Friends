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
        public static Action<Table>? OnUpdateTableRequest; //TODO: Might be unnecesarry

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
            ActivePlayerName = player.Name;
            player.IsCurrentlyActivePlayer = true;
            OnUpdateTableRequest?.Invoke(this);

            PlayerDecision decision;
            var timeoutTask = Task.Delay(ActionTimeout);
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

        async Task Play()
        {
            try
            {
                await EnoughplayersJoined.WaitAsync();
                lock (_playerJoinLock)
                {
                    _enoughPlayersSignaled = false;
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
            CurrentlyActivePlayer = Game.ClientInstance.GetPlayerFromName(name);
        }

        public static void HandleUpdateFromNetwork(int TableIndex, Table NetworkTable) // NOT FOR ADDING OR REMOVING PLAYERS, USE WHEN RECIEVING NETCODE "05" for clients
        {
            Table LocalTable = Game.ClientInstance.Tables[TableIndex];
            LocalTable.ChangeActivePlayerByName(NetworkTable.ActivePlayerName);
            LocalTable.Name = NetworkTable.Name;
            LocalTable.Pot = NetworkTable.Pot;
            LocalTable.Housecards = NetworkTable.Housecards;

            LocalTable.Players.Clear();
            if (NetworkTable.Players.Count > maxPlayers) { throw new ArgumentException("Recieved Network table has too mayn players."); }
            foreach (Player player in NetworkTable.Players)
            {
                Player LocalPlayer = Game.ClientInstance.GetPlayerFromName(player.Name);
                LocalTable.Players.Add(LocalPlayer);
                LocalPlayer.UpdateProperties(player);
            }

            LocalTable.IsGameActive = NetworkTable.IsGameActive;
            LocalTable.Pot = NetworkTable.Pot;
        }

        public static int GetTableIdByName(String name)
        {
            var table = Game.ClientInstance.Tables.FirstOrDefault(t => t.Name == name);
            return Game.ClientInstance.Tables.IndexOf(table);
        }
    }
}
