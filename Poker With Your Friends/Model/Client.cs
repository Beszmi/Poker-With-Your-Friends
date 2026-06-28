using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Client
    {
        public static Player CurrentPlayer { get; set; }
        public Player? ConatinedPlayer
        {
            get; set;
        }
    }
}
