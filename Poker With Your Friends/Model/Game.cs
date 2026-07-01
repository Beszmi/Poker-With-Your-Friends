using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model
{
    public class Game //Singleton
    {
        public static string PlayerfolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        public static string PlayerfilePath = Path.Combine(PlayerfolderPath, "players.xml");

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
            RefreshPlayerNames();
        }
        public static void RemovePlayer(Player player)
        {
            Players.Remove(player);
            RefreshPlayerNames();
        }

        public static ObservableCollection<String> PlayerNames { get; } = new ObservableCollection<String>();

        public static void RefreshPlayerNames()
        {
            PlayerNames.Clear();
            foreach (var player in Players)
            {
                PlayerNames.Add(player.Name);
            }
        }

        public static Player GetPlayerFromName(String name)
        {
            return Players.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException("Player not found");
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
