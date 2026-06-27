using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace Poker_With_Your_Friends.Model
{
    public class Player
    {
        private String name;

        public String Name
        {
            get { return name; }
            set { name = value; }
        }

        private int chips = 0;

        public int Chips
        {
            get { return chips; }
            set 
            { 
                if (value < 0) chips = 0;
                chips = value;
            }
        }

        private int ip;

        public int Ip
        {
            get { return ip; }
        }

        private List<Card> cards = new List<Card>();

        public List<Card> Cards { get { return cards; } }

        public Player(String name)
        {
            Name = name;
        }

        public Player(String name, int ip)
        {
            Name = name;
            this.ip = ip;
        }

        public Player(String name, int ip, int chips)
        {
            Name = name;
            this.ip = ip;
            Chips = chips;
        }

        public void AddCard(Card card)
        {
            cards.Add(card);
        }

        public void ClearCards()
        {
            cards.Clear();
        }
    }
}
