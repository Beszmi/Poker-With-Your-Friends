using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public event Action<String>? OnClientError;
    public event Action<Client>? OnServerConnected;
    public event Action? OnGameWindowOpening;

    public Game game = Game.ClientInstance;
    public String NewPlayerName { get; set; }

    public object? SelectedPlayerName { get; set; }

    public int NewServerPort { get; set; } = 5000;

    [ObservableProperty]
    public partial Visibility PlayerPickerVisible { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility ServerPickerVisible { get; set; } = Visibility.Visible;

    [ObservableProperty]
    public partial String ServerHostName { get; set; } = "localhost";

    [ObservableProperty]
    public partial int ServerPort { get; set; } = 5000;

    [ObservableProperty]
    public partial bool IsConnectButtonEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsRegisterButtonEnabled { get; set; } = true;

    [ObservableProperty]
    public partial String ServerText { get; set; } = "You are currently not connected to a server";

    public Client? client;
    public IPlayerStore PlayerStore { get; } = new PlayerStore();

    public MainWindowViewModel() { }
    
    public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string? playerName = SelectedPlayerName as string;
        if (string.IsNullOrEmpty(playerName)) return;

        PlayerStore.CurrentPlayer = game.GetPlayerFromName(playerName);
        client?.LoginPlayer(playerName);

        // 2. Pass the client to the GameWindow!
        OnGameWindowOpening?.Invoke();
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

            var playerTcs = new TaskCompletionSource<Player>();
            var errorTcs = new TaskCompletionSource<string>();

            void CheckNewPlayer(Player p)
            {
                if (p.Name == NewPlayerName)
                {
                    playerTcs.TrySetResult(p);
                }
            }

            void OnRegistrationError(string errorMessage)
            {
                errorTcs.TrySetResult(errorMessage);
            }

            game.OnPlayerAdded += CheckNewPlayer;
            client?.OnErrorReceived += OnRegistrationError;

            client?.RegisterNewPlayer(NewPlayerName);

            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(playerTcs.Task, timeoutTask, errorTcs.Task);

                if (completedTask == errorTcs.Task)
                {
                    return;
                }

                if (completedTask == timeoutTask)
                {
                    OnClientError?.Invoke("Registration timed out. Server did not respond.");
                    return;
                }

                Player newlyRegisteredPlayer = await playerTcs.Task;

                PlayerStore.CurrentPlayer = newlyRegisteredPlayer;

                OnGameWindowOpening?.Invoke();
                GameWindow newWindow = new GameWindow(client);
                newWindow.Activate();
            }
            finally
            {
                game.OnPlayerAdded -= CheckNewPlayer;
                client?.OnErrorReceived -= OnRegistrationError;
                IsRegisterButtonEnabled = true;
            }
        }
    }

    public async void ConnectToServer_Click(object sender, RoutedEventArgs e)
    {
        client?.Disconnect();
        client = new Client(ServerHostName, ServerPort, PlayerStore);

        IsConnectButtonEnabled = false;

        try
        {
            await client.ConnectAndRunAsync();

            ServerPickerVisible = Visibility.Collapsed;
            PlayerPickerVisible = Visibility.Visible;
            ServerText = $"You are currently connected to Server {client.Host} at Port: {client.Port}";
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

    public void LeaveServer()
    {
        client?.Disconnect();
        ServerPickerVisible = Visibility.Visible;
        PlayerPickerVisible = Visibility.Collapsed;
        ServerText = "You are currently not connected to Server";
    }
}
