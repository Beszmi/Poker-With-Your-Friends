using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        [XmlIgnore]
        private AutoResetEvent playerJoined = new AutoResetEvent(false);
        [XmlIgnore]
        private Deck deck = new Deck();
        [XmlIgnore]
        [ObservableProperty]
        private partial Player? CurrentlyActivePlayer { get; set; } = null;
        [XmlIgnore]
        private static int maxPlayers = 6;

        [XmlIgnore]
        public static Action<Table>? OnUpdateTableRequest;

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
        public partial ObservableCollection<Card> Housecards { get; set; }

        [XmlAttribute("Pot")]
        [ObservableProperty]
        public partial int Pot { get; set; }

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        [ObservableProperty]
        public partial ObservableCollection<Player> Players { get; set; }

        [XmlAttribute("IsGameActive")]
        [ObservableProperty]
        public partial bool IsGameActive { get; set; }

        public Table() { }

        public Table(String name)
        {
            Name = name;
        }

        public Table(Table t)
        {
            Name = t.Name;
            Round = t.Round;
            SmallBlind = t.SmallBlind;
            Pot = t.Pot;
            IsGameActive = t.IsGameActive;
            this.deck = t.deck;
            Players = t.Players;
            Housecards = t.Housecards;
        }

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
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() => Housecards.Clear());
            }
            else
            {
                Housecards.Clear();
            }
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
            if (player.HasFolded || !IsGameActive)
            {
                return;
            }
            player.IsCurrentlyActivePlayer = true;

            PlayerAction action = await _playerActionTcs.Task;
            OnUpdateTableRequest?.Invoke(this);
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
            CurrentlyActivePlayer = null; // Reset after action is taken
            OnUpdateTableRequest?.Invoke(this);
        }

        async Task Play()
        {
            IsGameActive = true;
            playerJoined.WaitOne();
            StartRound();
            DealToPlayers();

            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    Housecards.Add(deck.DrawCard());
                    Housecards.Add(deck.DrawCard());
                    Housecards.Add(deck.DrawCard());
                });
            }
            else
            {
                Housecards.Add(deck.DrawCard());
                Housecards.Add(deck.DrawCard());
                Housecards.Add(deck.DrawCard());
            }
            OnUpdateTableRequest?.Invoke(this);

            System.Diagnostics.Debug.WriteLine("Cards dealt to: " + Players.Count + " players");
            foreach (var player in Players)
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
    }
}
