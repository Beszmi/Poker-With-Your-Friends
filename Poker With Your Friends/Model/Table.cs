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

        /* -------------------------------------------------
         *  Properties
         *
         -------------------------------------------------*/
        [XmlIgnore]
        private AutoResetEvent playerJoined = new AutoResetEvent(false);
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
        public partial bool IsGameActive { get; set; }


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
                playerJoined.Set(); // Signal that a player has joined
            }
            if (Players.Count > maxPlayers)
            {
                throw new InvalidOperationException("Table is full. Cannot add more players.");
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

        private TaskCompletionSource<PlayerAction>? _playerActionTcs;

        [XmlIgnore]
        public TaskCompletionSource<PlayerAction>? PlayerActionTcs
        {
            get { return _playerActionTcs; }
            set { _playerActionTcs = value; }
        }
        public async Task WaitForPlayerActionAsync(Player player)
        {
            _playerActionTcs = new TaskCompletionSource<PlayerAction>();

            CurrentlyActivePlayer = player;
            ActivePlayerName = player.Name;
            if (player.HasFolded || !IsGameActive)
            {
                ActivePlayerName = string.Empty;
                return;
            }
            player.IsCurrentlyActivePlayer = true;
            OnUpdateTableRequest?.Invoke(this);

            PlayerAction action = await _playerActionTcs.Task;
            switch (action)
            {
                case PlayerAction.Call:
                    player.Call();
                    break;
                case PlayerAction.Raise:
                    // For simplicity, let's assume a fixed raise amount
                    int raiseAmount = 10; // This could be dynamic based on game rules
                    player.Raise(raiseAmount);
                    break;
                case PlayerAction.Fold:
                    player.Fold();
                    break;
            }

            player.IsCurrentlyActivePlayer = false;
            CurrentlyActivePlayer = null;
            ActivePlayerName = string.Empty;
            OnUpdateTableRequest?.Invoke(this);
        }

        async Task Play()
        {
            IsGameActive = true;
            playerJoined.WaitOne();
            StartRound();
            DealToPlayers();

            var dispatcher = App.MainDispatcher;

            Housecards.Add(deck.DrawCard());
            Housecards.Add(deck.DrawCard());
            Housecards.Add(deck.DrawCard());

            OnUpdateTableRequest?.Invoke(this);

            System.Diagnostics.Debug.WriteLine("Cards dealt to: " + Players.Count + " players");
            foreach (var player in Players.ToList())
            {
                foreach (var card in player.Cards)
                {
                    System.Diagnostics.Debug.WriteLine("Player " + player.Name + " has card: " + card.ToString());
                }
                System.Diagnostics.Debug.WriteLine("Waiting for action from: " + player.Name);
                await WaitForPlayerActionAsync(player);
            }
        }

        public void InitializeServerTable()
        {
            deck.Shuffle();
            Task.Run(() => Play());
        }

        public void HandlePlayerDisconnected(Player player)
        {
            if (Players.Contains(player))
            {
                if (CurrentlyActivePlayer == player &&
                    _playerActionTcs != null &&
                    !_playerActionTcs.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-folding for disconnected player: {player.Name}");
                    _playerActionTcs.TrySetResult(PlayerAction.Fold);
                }

                RemovePlayer(player);
                OnUpdateTableRequest?.Invoke(this);
            }
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
    }
}
