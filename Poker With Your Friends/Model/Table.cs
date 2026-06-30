using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Table: INotifyPropertyChanged
    {
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
        public int Round
        {
            get { return round; }
            set
            {
                round = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Round)));
            }
        }

        public int SmallBlind
        {
            get { return smallBlind; }
            set
            {
                smallBlind = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SmallBlind)));
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

        public Table(String name)
        {
            this.name = name;
            deck.Shuffle();

            housecards.Add(deck.DrawCard());
            housecards.Add(deck.DrawCard());
        }

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
            housecards.Clear();
        }
    }
}
