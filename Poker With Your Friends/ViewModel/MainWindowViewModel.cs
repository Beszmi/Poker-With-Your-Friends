using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public Game game = Game.Instance;
        public String NewPlayerName { get; set; }

        public String SelectedPlayerName { get; set; }

        public int NewServerPort { get; set; } = 5000;

        [ObservableProperty]
        public Visibility playerPickerVisible = Visibility.Collapsed;

        [ObservableProperty]
        public Visibility serverPickerVisible = Visibility.Visible;

        [ObservableProperty]
        public String serverHostName = "localhost";

        [ObservableProperty]
        public int serverPort = 5000;

        [ObservableProperty]
        public bool isConnectButtonEnabled = true;

        private Client? client;

        public MainWindowViewModel()
        {
        }
        //TODO: Move this to server code!
        
        public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            client.ContainedPlayer = game.GetPlayerFromName(SelectedPlayerName);
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
                game.AddPlayer(newPlayer);
                //SavePlayersToXml(Game.PlayerfilePath);

                client.ContainedPlayer = newPlayer;

                GameWindow newWindow = new GameWindow(client);
                newWindow.Activate();
            }
        }

        public void ConnectToServer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            client = new Client(ServerHostName, ServerPort);
            isConnectButtonEnabled = false;
            Task.Run(async () => await client.ConnectAndRunAsync());
            isConnectButtonEnabled = true;
            ServerPickerVisible = Visibility.Collapsed;
            PlayerPickerVisible = Visibility.Visible;
        }
    }
}
