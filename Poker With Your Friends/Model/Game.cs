using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlRoot("Game")]
    public class Game //Singleton
    {
        [XmlIgnore]
        public static string PlayerfolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        [XmlIgnore]
        public static string PlayerfilePath = Path.Combine(PlayerfolderPath, "players.xml");

        [XmlIgnore]
        private static Game instance;

        public Game()
        {
        }

        [XmlIgnore]
        public static Game Instance
        {
            get
            {
                instance ??= new Game();
                return instance;
            }
        }

        /* 
         * Players
        */

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public ObservableCollection<Player> Players { get; set; } = new ObservableCollection<Player>();

        public void AddPlayer(Player player)
        {
            Players.Add(player);
            RefreshPlayerNames();
        }
        public void RemovePlayer(Player player)
        {
            Players.Remove(player);
            RefreshPlayerNames();
        }

        [XmlIgnore]
        public ObservableCollection<String> PlayerNames { get; } = new ObservableCollection<String>();

        public void RefreshPlayerNames()
        {
            PlayerNames.Clear();
            foreach (var player in Players)
            {
                PlayerNames.Add(player.Name);
            }
        }

        public Player GetPlayerFromName(String name)
        {
            return Players.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException("Player not found");
        }

        /* 
         * TABLES
        */
        [XmlArray("Tables")]
        [XmlArrayItem("Table")]
        public ObservableCollection<Table> Tables { get; set; } = new ObservableCollection<Table>();

        public void AddTable(String name)
        {
            Table t = new Table(name);
            Tables.Add(t);
            Server.InitializeServerTable(t);
        }

        public void RemoveTable(Table table)
        {
            Tables.Remove(table);
        }

        public void GameStateUpdate(Game UpdatedGame)
        {
            var dispatcher = App.MainDispatcher;

            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() => ApplyState(UpdatedGame));
            }
            else
            {
                ApplyState(UpdatedGame);
            }
        }
        private void ApplyState(Game UpdatedGame)
        {
            Players.Clear();
            foreach (var player in UpdatedGame.Players)
            {
                Players.Add(player);
            }

            Tables.Clear();
            foreach (var table in UpdatedGame.Tables)
            {
                Tables.Add(table);
            }

            RefreshPlayerNames();
        }
    }
}
