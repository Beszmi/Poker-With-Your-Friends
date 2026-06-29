using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlType("Player")]
    public class Player
    {
        public static string folderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        public static string filePath = Path.Combine(folderPath, "players.xml");

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

        public Player() {}

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

        private String? currentTableName;
        [XmlIgnore]
        public String? CurrentTableName
        {
            get { return currentTableName; }
            set { currentTableName = value; }
        }

    }
}
