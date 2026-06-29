using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.ViewModel
{
    public class MainWindowViewModel
    {
        public Game game = Game.Instance;
        public string NewPlayerName { get; set; }

        //TODO: Move this to server code!
        public MainWindowViewModel()
        {
            ReadPlayersFromXml(Player.filePath);
        }
        public void ReadPlayersFromXml(string xmlFilePath)
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

        public void SavePlayersToXml(string xmlFilePath)
        {
            if (!Directory.Exists(Player.folderPath))
            {
                Directory.CreateDirectory(Player.folderPath);
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
            SavePlayersToXml(Player.filePath);
        }
        public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Client.CurrentPlayer = new Player(NewPlayerName);
            Game.AddPlayer(Client.CurrentPlayer);
            GameWindow newWindow = new GameWindow();
            newWindow.Activate();
        }

        public void StartServerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ServerWindow newWindow = new ServerWindow();
            newWindow.Activate();
        }
    }
}
