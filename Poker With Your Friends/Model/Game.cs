using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public static ObservableCollection<Player> Players { get; } = [];

        public static void AddPlayer(Player player)
        {
            Players.Add(player);
        }
        public static void RemovePlayer(Player player)
        {
            Players.Remove(player);
        }

        public static ObservableCollection<Table> Tables { get; } = [];

        public static void AddTable(String name)
        {
            Tables.Add(new Table(name));
        }

        public static void RemoveTable(Table table)
        {
            Tables.Remove(table);
        }
    }
}
