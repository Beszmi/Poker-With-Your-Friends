using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Table
    {
        private String name;
        private int round;
        private int smallBlind;
        private Deck deck = new Deck();
        private int pot = 0;
        public String Name
        {
            get { return name; }
            set { name = value; }
        }
        public int Round
        {
            get { return round; }
        }

        public int SmallBlind
        {
            get { return smallBlind; }
        }

        public int Pot
        {
            get { return pot; }
        }

        private List<Player> players = new List<Player>();

        public List<Player> Players
        {
            get { return players; }
        }

        public Table(String name)
        {
            this.name = name;
        }

        public void AddPlayer(Player player)
        {
            if (!player.IsAtTable)
            {
                players.Add(player);
                player.IsAtTable = true;
            }
        }

        public void RemovePlayer(Player player)
        {
            if (player.IsAtTable)
            {
                players.Remove(player);
                player.IsAtTable = false;
            }
        }
    }
}
