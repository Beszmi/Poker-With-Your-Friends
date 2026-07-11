using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using static Poker_With_Your_Friends.Model.Table;

namespace Poker_With_Your_Friends.ViewModel;

public partial class InGamePageViewModel : ObservableObject
{
    private Client? client;
    public IPlayerStore? PlayerStore { get; private set; }

    [ObservableProperty]
    public partial Table Table { get; set; }

    [ObservableProperty]
    public partial bool LeaveTableButtonEnabled { get; set; } = true;

    [ObservableProperty]
    public partial Visibility IsJoinButtonVisible { get; set; }

    [ObservableProperty]
    public partial Visibility IsLeaveButtonVisible { get; set; }

    [ObservableProperty]
    public partial Visibility IsplayerOnOwnTable { get; set; }

    [ObservableProperty]
    public partial String? TableText { get; set; } = "Empty text";

    [ObservableProperty]
    public partial bool PlayerActionButtonsEnabled { get; set; } = false;

    public ObservableCollection<Card>? MyCards => PlayerStore?.CurrentPlayer?.Cards;

    private DispatcherQueue _dispatcherQueue;

    // TIMER
    [ObservableProperty]
    public partial TableTimer? Timer { get; set; }

    public string TimerRemainingText => $"{Timer?.Remaining.TotalSeconds:F0}";

    public double TimerProgressValue => Timer?.Remaining.TotalSeconds ?? 0;

    public double TimerProgressMaximum => Timer?.Total.TotalSeconds ?? 60;

    partial void OnTimerChanged(TableTimer? oldValue, TableTimer? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTimerPropertyChanged;
            oldValue.Expired -= OnTimerExpired;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnTimerPropertyChanged;
            newValue.Expired += OnTimerExpired;
        }

        NotifyTimerDisplayProperties();
    }

    private void OnTimerExpired()
    {
        PlayerActionButtonsEnabled = false;
    }

    private void OnTimerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TableTimer.Remaining) or nameof(TableTimer.Total))
        {
            NotifyTimerDisplayProperties();
        }
    }

    private void NotifyTimerDisplayProperties()
    {
        OnPropertyChanged(nameof(TimerRemainingText));
        OnPropertyChanged(nameof(TimerProgressValue));
        OnPropertyChanged(nameof(TimerProgressMaximum));
    }

    public InGamePageViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }
    public void Initialize(Client client, Table table)
    {
        this.client = client;
        PlayerStore = client.PlayerStore;
        this.Table = table;
        Timer = client.TimerService.GetOrCreateTimer(table);

        RefreshLocalState();
    }

    public void NetworkTableUpdated()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var liveTable = Game.ClientInstance.Tables.FirstOrDefault(t => t.Name == Table?.Name);
            if (liveTable != null)
            {
                Table = liveTable;
                Timer = client?.TimerService.GetOrCreateTimer(liveTable);
                if (PlayerStore?.CurrentTable?.Name == liveTable.Name)
                {
                    PlayerStore.CurrentTable = liveTable;
                }

                RefreshLocalState();
            }
        });
    }

    public void RefreshLocalState()
    {
        if (PlayerStore?.CurrentPlayer == null || Table == null) return;

        var updatedPlayer = Table.Players.FirstOrDefault(p => p.Name == PlayerStore.CurrentPlayer.Name);
        if (updatedPlayer != null)
        {
            PlayerStore.CurrentPlayer = updatedPlayer;
        }

        OnPropertyChanged(nameof(MyCards));

        bool isAtThisTable = Table.Players.Any(p => p.Name == PlayerStore.CurrentPlayer.Name);

        if (isAtThisTable)
        {
            IsplayerOnOwnTable = Visibility.Visible;
            IsJoinButtonVisible = Visibility.Collapsed;
            IsLeaveButtonVisible = Visibility.Visible;

            PlayerActionButtonsEnabled = !string.IsNullOrEmpty(Table.ActivePlayerName)
                && Table.ActivePlayerName == PlayerStore.CurrentPlayer.Name;
        }
        else
        {
            IsplayerOnOwnTable = Visibility.Collapsed;
            IsJoinButtonVisible = Visibility.Visible;
            IsLeaveButtonVisible = Visibility.Collapsed;
            PlayerActionButtonsEnabled = false;
        }
        TableText = Table.Name + " Active: " + Table.IsGameActive + "Current player: " + Table.ActivePlayerName;
    }

    public void CallButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPlayerAction(PlayerAction.Call, 0);
    }
    public void RaiseButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPlayerAction(PlayerAction.Raise, 10);
    }
    public void FoldButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPlayerAction(PlayerAction.Fold, 0);
    }

    private void SubmitPlayerAction(PlayerAction action, int amount)
    {
        if (!PlayerActionButtonsEnabled || client == null)
        {
            return;
        }

        client.SendPlayerAction(action, amount);
        Timer?.StopTimer();
        PlayerActionButtonsEnabled = false;
    }
}
