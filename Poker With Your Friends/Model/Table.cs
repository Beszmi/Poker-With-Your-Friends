using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlRoot("Table")]
    public partial class Table : INotifyPropertyChanged
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
        private String name;
        [XmlIgnore]
        private int round = 0;
        [XmlIgnore]
        private int smallBlind;
        [XmlIgnore]
        private Deck deck = new Deck();
        [XmlIgnore]
        private ObservableCollection<Card> housecards = new ObservableCollection<Card>();
        [XmlIgnore]
        private ObservableCollection<Player> players = new ObservableCollection<Player>();
        [XmlIgnore]
        private int pot = 0;
        [XmlIgnore]
        private Player? CurrentlyActivePlayer = null;
        [XmlIgnore]
        private static int maxPlayers = 6;
        [XmlIgnore]
        private bool isGameActive = false;

        [XmlAttribute("Name")]
        public String Name
        {
            get { return name; }
            set
            { 
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [XmlAttribute("Round")]
        public int Round
        {
            get { return round; }
            set
            {
                round = value;
                OnPropertyChanged(nameof(Round));
            }
        }

        [XmlAttribute("SmallBlind")]
        public int SmallBlind
        {
            get { return smallBlind; }
            set
            {
                smallBlind = value;
                OnPropertyChanged(nameof(SmallBlind));
            }
        }

        [XmlArray("Housecards")]
        [XmlArrayItem("Housecard")]
        public ObservableCollection<Card> Housecards
        {
            get { return housecards; }
            set { housecards = value; }
        }

        [XmlAttribute("Pot")]
        public int Pot
        {
            get { return pot; }
            set { pot = value; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public ObservableCollection<Player> Players
        {
            get { return players;}
            set
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Players)));
                players = value;
            }
        }

        [XmlAttribute("IsGameActive")]
        public bool IsGameActive
        {
            get { return isGameActive; }
            set
            {
                isGameActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGameActive)));
            }
        }

        public Table() { }

        public Table(String name)
        {
            this.name = name;
        }

        public Table(Table t)
        {
            this.name = t.Name;
            this.round = t.Round;
            this.smallBlind = t.SmallBlind;
            this.pot = t.Pot;
            this.isGameActive = t.IsGameActive;
            this.deck = t.deck;
            this.players = t.players;
            this.Housecards = t.Housecards;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            var dispatcher = App.MainDispatcher;

            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
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
            pot = 0;
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() => housecards.Clear());
            }
            else
            {
                housecards.Clear();
            }
        }

        public void DealToPlayers()
        {
            foreach (var player in Players)
            {
                var dispatcher = App.MainDispatcher;
                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(() => player.ClearCards());
                }
                else
                {
                    player.ClearCards();
                }

                Card card = deck.DrawCard();
                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(() => player.AddCard(card));
                }
                else
                {
                    player.AddCard(card);
                }

                Thread.Sleep(100); // Simulate delay for dealing cards
            }
            foreach (var player in Players) //2nd card
            {
                var dispatcher = App.MainDispatcher;
                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(() => player.AddCard(deck.DrawCard()));
                }
                else
                {
                    player.AddCard(deck.DrawCard());
                }
                Thread.Sleep(100);
            }
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
                    housecards.Add(deck.DrawCard());
                    housecards.Add(deck.DrawCard());
                    housecards.Add(deck.DrawCard());
                });
            }
            else
            {
                housecards.Add(deck.DrawCard());
                housecards.Add(deck.DrawCard());
                housecards.Add(deck.DrawCard());
            }

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
