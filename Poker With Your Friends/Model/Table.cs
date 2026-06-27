using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Table
    {
        private Deck deck = new Deck();
        private int pot = 0;
        public int Pot
        {
            get { return pot; }
        }

        private List<Player> players = new List<Player>();

        public List<Player> Players
        {
            get { return players; }
        }
    }
}
