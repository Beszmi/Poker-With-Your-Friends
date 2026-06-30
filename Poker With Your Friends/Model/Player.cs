using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    public delegate void PlayerButtonsChanged(bool buttonsEnabled);
    [XmlType("Player")]
    public class Player
    {
        public event PlayerButtonsChanged? OnPlayerButtonsChanged;

        private String name;
        [XmlAttribute("Name")]
        public String Name
        {
            get { return name; }
            set { name = value; }
        }

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
        [XmlIgnore]
        public int Ip { get; set; }

        private List<Card> cards = new List<Card>();
        [XmlIgnore]
        public List<Card> Cards { get { return cards; } }

        public Player() { }

        public Player(String name)
        {
            Name = name;
        }

        public Player(String name, int ip)
        {
            Name = name;
            this.Ip = ip;
        }

        public Player(String name, int ip, int chips)
        {
            Name = name;
            this.Ip = ip;
            Chips = chips;
        }

        public Player(Player p)
        {
            this.name = p.name;
            this.chips = p.chips;
        }

        public void AddCard(Card card)
        {
            cards.Add(card);
        }

        public void ClearCards()
        {
            cards.Clear();
        }

        private bool isAtTable = false;
        [XmlIgnore]
        public bool IsAtTable
        {
            get { return isAtTable; }
            set { isAtTable = value; }
        }

        private Table? currentTable;
        [XmlIgnore]
        public Table? CurrentTable
        {
            get { return currentTable; }
            set { currentTable = value; }
        }

        [XmlIgnore]
        public bool HasFolded { get; set; } = false;

        [XmlIgnore]
        public int CurrentBet { get; set; } = 0;

        [XmlIgnore]
        public bool CanLeaveGame { 
            get
            {
                if (isAtTable)
                {
                    return HasFolded || !CurrentTable.IsGameActive;
                }
                return true; //Fallback: If the player is not at a table, they can leave the game.
            }
        }

        private bool isCurrentlyActivePlayer = false;
        [XmlIgnore]
        public bool IsCurrentlyActivePlayer 
        { 
            get { return IsCurrentlyActivePlayer; } 
            set
            {
                OnPlayerButtonsChanged?.Invoke(value);
                isCurrentlyActivePlayer = value;
                System.Diagnostics.Debug.WriteLine("player's active state changed to: " + value);
            }
        }

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
    }
}
