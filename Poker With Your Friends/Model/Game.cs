using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Game //Singleton
    {
        private static Game instance;

        private Game()
        {
        }

        public static Game Instance
        {
            get
            {
                instance ??= new Game();
                return instance;
            }
        }

        public static List<Player> Players { get; } = [];

        public static void AddPlayer(Player player)
        {
            Players.Add(player);
        }
        public static void RemovePlayer(Player player)
        {
            Players.Remove(player);
        }

        private static List<Table> tables = new List<Table>();
        public static List<Table> Tables { get; } = [];

        public static void AddTable(Table table)
        {
            tables.Add(table);
        }

        public static void RemoveTable(Table table)
        {
            tables.Remove(table);
        }
    }
}
