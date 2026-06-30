using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public partial class Table : INotifyPropertyChanged
    {
        public enum PlayerAction
        {
            Call,
            Raise,
            Fold
        }

        private AutoResetEvent playerJoined = new AutoResetEvent(false);
        private String name;
        private int round = 0;
        private int smallBlind;
        private Deck deck = new Deck();
        private ObservableCollection<Card> housecards = new ObservableCollection<Card>();
        private ObservableCollection<Player> players = new ObservableCollection<Player>();
        private int pot = 0;
        private Player? CurrentlyActivePlayer = null;
        private static int maxPlayers = 6;
        private bool isGameActive = false;
        public String Name
        {
            get { return name; }
            set
            { 
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public int Round
        {
            get { return round; }
            set
            {
                round = value;
                OnPropertyChanged(nameof(Round));
            }
        }

        public int SmallBlind
        {
            get { return smallBlind; }
            set
            {
                smallBlind = value;
                OnPropertyChanged(nameof(SmallBlind));
            }
        }

        public ObservableCollection<Card> Housecards
        {
            get { return housecards; }
        }

        public int Pot
        {
            get { return pot; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Player> Players
        {
            get { return players;}
            private set
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Players)));
                players = value;
            }
        }
        public bool IsGameActive
        {
            get { return isGameActive; }
            set
            {
                isGameActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGameActive)));
            }
        }

        private DispatcherQueue _dispatcherQueue;
        public Table(String name)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.name = name;
            deck.Shuffle();

            housecards.Add(deck.DrawCard());
            housecards.Add(deck.DrawCard());
            Task.Run(() => Play());
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
            {
                _dispatcherQueue.TryEnqueue(() =>
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
            if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
            {
                _dispatcherQueue.TryEnqueue(() => housecards.Clear());
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
                player.ClearCards();
                player.AddCard(deck.DrawCard());
                Thread.Sleep(100); // Simulate delay for dealing cards
            }
            foreach (var player in Players) //2nd card
            {
                player.AddCard(deck.DrawCard());
                Thread.Sleep(100);
            }
        }

        private TaskCompletionSource<PlayerAction>? _playerActionTcs;

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
            System.Diagnostics.Debug.WriteLine("player's current state should be changed to: " + false);
            CurrentlyActivePlayer = null; // Reset after action is taken
        }

        async Task Play()
        {
            System.Diagnostics.Debug.WriteLine("Table playwaiting for player joined");
            IsGameActive = true;
            playerJoined.WaitOne();
            System.Diagnostics.Debug.WriteLine("player joined");
            StartRound();
            System.Diagnostics.Debug.WriteLine("Round started");
            DealToPlayers();
            System.Diagnostics.Debug.WriteLine("Cards dealt to: " + Players.Count + " players");
            foreach (var player in Players)
            {
                System.Diagnostics.Debug.WriteLine("Waiting for action from: " + player.Name);
                await WaitForPlayerActionAsync(player);
            }
        }
    }
}
