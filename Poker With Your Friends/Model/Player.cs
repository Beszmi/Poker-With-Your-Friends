using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlType("Player")]
    public partial class Player : ObservableObject
    {
        [XmlAttribute("Name")]
        [ObservableProperty]
        public partial String Name { get; set; }

        private int chips = 0;
        [XmlAttribute("Chips")]
        public int Chips
        {
            get { return chips; }
            set
            {
                chips = Math.Max(0, value);
            }
        }

        [XmlArray("Cards")]
        [XmlArrayItem("Card")]
        [ObservableProperty]
        public partial ObservableCollection<Card> Cards { get; set; } = new ObservableCollection<Card>();

        public Player() { }

        public Player(String name)
        {
            Name = name;
        }

        public Player(String name, int chips)
        {
            Name = name;
            Chips = chips;
        }

        public Player(Player p)
        {
            Name = p.Name;
            this.chips = p.chips;
        }

        public void AddCard(Card card)
        {
            Cards.Add(card);
        }

        public void ClearCards()
        {
            Cards.Clear();
        }

        [XmlIgnore]
        [ObservableProperty]
        private bool isAtTable = false;

        [XmlAttribute("hasFolded")]
        [ObservableProperty]
        private bool hasFolded = false;

        [XmlIgnore]
        [ObservableProperty]
        public partial int CurrentBet { get; set; } = 0;

        [XmlAttribute("IsCurrentlyActivePlayer")]
        [ObservableProperty]
        public partial bool IsCurrentlyActivePlayer { get; set; }

        public void Fold() { HasFolded = true; }

        public void Call()
        {
            System.Diagnostics.Debug.WriteLine("player calling not implemented");
        }

        public void Raise(int amount)
        {
            System.Diagnostics.Debug.WriteLine("player raising not implemented");
        } 

        public void Lose()
        {
            Chips-= CurrentBet;
            if (Chips <= 0)
            {
                throw new NotImplementedException(); // Player is out of chips and loses the game
            }
        }
        public void Win(int amount)
        {
            Chips += amount;
        }

        public void UpdateProperties(Player NewPlayer)
        {
            Chips = NewPlayer.Chips;
            // IsAtTable Should be handled by joining and leaving code
            HasFolded = NewPlayer.HasFolded;
            CurrentBet = NewPlayer.CurrentBet;
            IsCurrentlyActivePlayer = NewPlayer.IsCurrentlyActivePlayer;

            Cards.Clear();
            foreach (var card in NewPlayer.Cards)
            {
                Cards.Add(card);
            }
        }
    }
}
