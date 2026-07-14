using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;

namespace Poker_With_Your_Friends.ViewModel;

internal partial class ServerWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPlayerChips))]
    public partial object? SelectedPlayerName { get; set; }

    [ObservableProperty]
    public partial String? NewName { get; set; }

    [ObservableProperty]
    public partial int? NewChips { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string>? ServerLogs { get; set; } = new ObservableCollection<string>();

    private Server server;
    private readonly DispatcherQueue _dispatcherQueue;

   //public Action<String>? OnPlayerEdit;

    public Game game = Game.ServerInstance;

    public ServerWindowViewModel(Server server, DispatcherQueue dispatcherQueue)
    {
        this.server = server;
        _dispatcherQueue = dispatcherQueue;
        game.ReadPlayersFromXml(Game.PlayerfilePath);

        server.OnServerLoggedEvent += Log;
    }

    public int SelectedPlayerChips
    {
        get
        {
            string? name = SelectedPlayerName as string;
            if (string.IsNullOrEmpty(name)) return 0;

            var player = game.GetPlayerFromName(name);
            return player != null ? player.Chips : 0;
        }
    }

    public void DeletePlayer()
    {
        string? name = SelectedPlayerName as string;
        if (name != null)
        {
            game.RemovePlayer(game.GetPlayerFromName(name));
            SelectedPlayerName = null;
        }
    }

    public void EditPlayer()
    {
        string? name = SelectedPlayerName as string;
        if (name == null) return;

        var player = game.GetPlayerFromName(name);
        bool chipsChanged = false;

        if (NewChips != null && NewChips != player.Chips)
        {
            player.Chips = (int)NewChips;
            chipsChanged = true;
        }

        if (!string.IsNullOrEmpty(NewName) && NewName != name)
        {
            player.Name = NewName;
            game.RefreshPlayerNames();

            SelectedPlayerName = NewName;
        }
        else if (chipsChanged)
        {
            OnPropertyChanged(nameof(SelectedPlayerChips));
        }

        //OnPlayerEdit.Invoke()
    }
    
    public void StopServer()
    {
        server?.Stop();
    }

    public void Log(string message)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (ServerLogs.Count > 25)
            {
                ServerLogs.RemoveAt(0);
            }
            ServerLogs.Add(message);
        });

        System.Diagnostics.Debug.WriteLine("Logged: " + message);
    }
    public void FlipDebug()
    {
        server.debugMessages = !server.debugMessages;
    }
}
