using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    [XmlRoot("Game")]
    public class Game //Singleton
    {
        public event Action<Player>? OnPlayerAdded;
        public event Action<Table>? OnTableAdded;

        [XmlIgnore]
        public static string PlayerfolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Poker_With_Your_Friends");

        [XmlIgnore]
        public static string PlayerfilePath = Path.Combine(PlayerfolderPath, "players.xml");

        [XmlIgnore]
        private static Game Clientinstance;
        [XmlIgnore]
        private static Game Serverinstance;

        [XmlIgnore]
        public bool ServerMode = false;

        public Game() { }

        [XmlIgnore]
        public static Game ClientInstance
        {
            get
            {
                Clientinstance ??= new Game();
                return Clientinstance;
            }
        }

        [XmlIgnore]
        public static Game ServerInstance
        {
            get
            {
                Serverinstance ??= new Game();
                return Serverinstance;
            }
        }

        /* 
         * Players
        */

        [XmlArray("Players")]
        [XmlArrayItem("Player")]
        public ObservableCollection<Player> Players { get; set; } = new ObservableCollection<Player>();

        public void AddPlayer(Player player, bool isServer)
        {
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    Players.Add(player);
                    RefreshPlayerNames();
                    OnPlayerAdded?.Invoke(player);
                });
            }
            else
            {
                Players.Add(player);
                RefreshPlayerNames();
                OnPlayerAdded?.Invoke(player);
            }
        }

        public void RemovePlayer(Player player)
        {
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    Players.Remove(player);
                    RefreshPlayerNames();
                });
            }
            else
            {
                Players.Remove(player);
                RefreshPlayerNames();
            }
        }

        [XmlIgnore]
        public ObservableCollection<String> PlayerNames { get; } = new ObservableCollection<String>();

        public void RefreshPlayerNames()
        {
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(RefreshNamesInternal);
            }
            else
            {
                RefreshNamesInternal();
            }
        }

        private void RefreshNamesInternal()
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

        public void AddTable(String message, bool isServer)
        {
            if (!isServer)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Table));
                {
                    if (serializer.Deserialize(new StringReader(message)) is Table deserializedTable)
                    {
                        Tables.Add(deserializedTable);
                    }
                }
                return;
            }
            throw new ArgumentException();
        }

        public void AddTable(Table table)
        {
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    Tables.Add(table);
                    OnTableAdded?.Invoke(table);
                });
            }
            else
            {
                Tables.Add(table);
                OnTableAdded?.Invoke(table);
            }
            Server.InitializeServerTable(table);
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
            else if (dispatcher != null && dispatcher.HasThreadAccess)
            {
                ApplyState(UpdatedGame);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CRITICAL: App.MainDispatcher is null! Cannot safely update UI.");
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

        public void ReadPlayersFromXml(String xmlFilePath)
        {
            if (!Directory.Exists(Game.PlayerfolderPath))
            {
                Directory.CreateDirectory(Game.PlayerfolderPath);
            }
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));

            if (!File.Exists(xmlFilePath))
            {
                ObservableCollection<Player> empty = new ObservableCollection<Player>();

                using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, empty);
                }
                return;
            }

            ObservableCollection<Player>? deserializedList = null;

            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                deserializedList = serializer.Deserialize(fileStream) as ObservableCollection<Player>;
            }
            if (deserializedList != null)
            {
                var dispatcher = App.MainDispatcher;

                Action loadAction = () =>
                {
                    Players.Clear();
                    foreach (var player in deserializedList)
                    {
                        Players.Add(player);
                    }
                    RefreshPlayerNames();
                };

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(() => loadAction());
                }
                else
                {
                    loadAction();
                }
            }
        }
        public void SavePlayersToXml(String xmlFilePath)
        {
            if (!Directory.Exists(Game.PlayerfolderPath))
            {
                Directory.CreateDirectory(Game.PlayerfolderPath);
            }

            if (Players == null || Players.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"SAVE FAILED: Players empty");
                return;
            }
            foreach (var player in Players)
            {
                player.IsCurrentlyActivePlayer = false;
                player.IsAtTable = false;
                player.HasFolded = false;
                player.ClearCards();
                player.CurrentBet = 0;
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));
                using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, Players);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SAVE FAILED: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"INNER EXCEPTION: {ex.InnerException.Message}");
                }
            }
        }

        public bool DoesPlayerAlreadyExist(String name)
        {
            try
            {
                GetPlayerFromName(name);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        public bool IsTableNameTaken(String tableName)
        {
            foreach(Table table in Tables)
            {
                if (table.Name == tableName) return true;
            }
            return false;
        }
    }
}
