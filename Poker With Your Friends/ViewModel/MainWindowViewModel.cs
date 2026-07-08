using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace Poker_With_Your_Friends.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public event Action<String>? OnClientError;
        public event Action<Client>? OnServerConnected;

        public Game game = Game.ClientInstance;
        public String NewPlayerName { get; set; }

        public object? SelectedPlayerName { get; set; }

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

        [ObservableProperty]
        public bool isRegisterButtonEnabled = true;

        public Client? client;
        public IPlayerStore PlayerStore { get; } = new PlayerStore();

        public MainWindowViewModel() { }
        
        public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string? playerName = SelectedPlayerName as string;
            if (string.IsNullOrEmpty(playerName)) return;

            PlayerStore.CurrentPlayer = game.GetPlayerFromName(playerName);

            // 2. Pass the client to the GameWindow!
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
            _ = RegisterNewPlayerClickAsync();
        }

        public async Task RegisterNewPlayerClickAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewPlayerName))
            {
                if (game.DoesPlayerAlreadyExist(NewPlayerName))
                {

                }
                IsRegisterButtonEnabled = false;

                var tcs = new TaskCompletionSource<Player>();

                void CheckNewPlayer(Player p)
                {
                    if (p.Name == NewPlayerName)
                    {
                        tcs.TrySetResult(p);
                    }
                }

                game.OnPlayerAdded += CheckNewPlayer;

                client?.RegisterNewPlayer(NewPlayerName);

                try
                {
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        OnClientError?.Invoke("Registration timed out. Server did not respond.");
                        return;
                    }

                    Player newlyRegisteredPlayer = await tcs.Task;

                    PlayerStore.CurrentPlayer = newlyRegisteredPlayer;

                    GameWindow newWindow = new GameWindow(client);
                    newWindow.Activate();
                }
                finally
                {
                    game.OnPlayerAdded -= CheckNewPlayer;
                    IsRegisterButtonEnabled = true;
                }
            }
        }

        public async void ConnectToServer_Click(object sender, RoutedEventArgs e)
        {
            client = new Client(ServerHostName, ServerPort, PlayerStore);

            IsConnectButtonEnabled = false;

            try
            {
                await client.ConnectAndRunAsync();

                ServerPickerVisible = Visibility.Collapsed;
                PlayerPickerVisible = Visibility.Visible;
            }
            catch (Exception ex)
            {
                OnClientError?.Invoke($"Failed to connect to server: {ex.Message}");
            }
            finally
            {
                OnServerConnected?.Invoke(client);
                IsConnectButtonEnabled = true;
            }
        }
    }
}
