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
        private int round;
        private int smallBlind;
        private Deck deck = new Deck();
        private ObservableCollection<Card> housecards = new ObservableCollection<Card>();
        private int pot = 0;
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

        private ObservableCollection<Player> players = new ObservableCollection<Player>();

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

        public Table(String name)
        {
            this.name = name;
        }

        public void AddPlayer(Player player)
        {
            if (!player.IsAtTable)
            {
                Players.Add(player);
                player.IsAtTable = true;
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
    }
}
