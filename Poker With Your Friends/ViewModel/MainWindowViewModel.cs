using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.ViewModel
{
    public class MainWindowViewModel
    {
        public Game game = Game.Instance;
        public String NewPlayerName { get; set; }

        public String SelectedPlayerName { get; set; }

        public int NewServerPort { get; set; } = 5000;

        public MainWindowViewModel()
        {
            ReadPlayersFromXml(Game.PlayerfilePath);
        }
        //TODO: Move this to server code!
        public void ReadPlayersFromXml(String xmlFilePath)
        {
            if (!File.Exists(xmlFilePath))
                return;

            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));
            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open))
            {
                ObservableCollection<Player>? deserializedList = serializer.Deserialize(fileStream) as ObservableCollection<Player>;
                if (deserializedList != null)
                {
                    Game.Players.Clear();
                    foreach (var player in deserializedList)
                    {
                        Game.AddPlayer(player);
                    }
                }
            }
        }

        //TODO: Move this to server code!
        public void SavePlayersToXml(String xmlFilePath)
        {
            if (!Directory.Exists(Game.PlayerfolderPath))
            {
                Directory.CreateDirectory(Game.PlayerfolderPath);
            }

            if (Game.Players == null || Game.Players.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"SAVE FAILED: Players empty");
                return;
            }
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));
                using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, Game.Players);
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
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            //TODO: Move this to server code!
            SavePlayersToXml(Game.PlayerfilePath);
        }
        public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Client client = new Client("127.0.0.1", 5000);
            client.ContainedPlayer = Game.GetPlayerFromName(SelectedPlayerName);
            Task.Run(async () => await client.ConnectAndRunAsync());
            GameWindow newWindow = new GameWindow(client);
            newWindow.Activate();
        }

        public void StartServerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Server server = new Server(NewServerPort);
            Task.Run(async () => await server.StartAsync());
            ServerWindow newWindow = new ServerWindow(server);
            newWindow.Activate();
        }

        public void RegisterNewPlayerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NewPlayerName))
            {
                Player newPlayer = new Player(NewPlayerName);
                Game.AddPlayer(newPlayer);
                SavePlayersToXml(Game.PlayerfilePath);

                Client client = new Client("127.0.0.1", 5000);
                client.ContainedPlayer = newPlayer;
                Task.Run(async () => await client.ConnectAndRunAsync());

                GameWindow newWindow = new GameWindow(client);
                newWindow.Activate();
            }
        }
    }
}
