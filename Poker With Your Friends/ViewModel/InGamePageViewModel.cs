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
    public partial bool PlayerActionButtonsEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsRaiseButtonEnabled { get; set; } = false;

    [ObservableProperty]
    public partial int RaiseMin { get; set; } = 0;

    [ObservableProperty]
    public partial int RaiseMax { get; set; } = 0;

    [ObservableProperty]
    public partial int SelectedRaiseValue { get; set; } = 0;

    [ObservableProperty]
    public partial String CallButtonText { get; set; } = "Call";

    [ObservableProperty]
    public partial String CurrentPlayerHandName { get; set; } = "No hand";

    public ObservableCollection<Player> OpponentPlayers { get; } = new ObservableCollection<Player>();

    [ObservableProperty]
    public partial Visibility OpponentCardsRevealed { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility OpponentCardsNotRevealed { get; set; } = Visibility.Visible;

    [ObservableProperty]
    public partial Visibility CardsDealt { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility IsCurrentPlayerWinner { get; set; } = Visibility.Collapsed;

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

        CardsDealt = Table.Players.Any(p => p.Cards.Count > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

        RebuildOpponentPlayers(isAtThisTable);

        if (isAtThisTable)
        {
            IsplayerOnOwnTable = Visibility.Visible;
            IsJoinButtonVisible = Visibility.Collapsed;
            IsLeaveButtonVisible = Visibility.Visible;

            if (Table.HandOver)
            {
                OpponentCardsRevealed = Visibility.Visible;
                OpponentCardsNotRevealed = Visibility.Collapsed;
                IsCurrentPlayerWinner = PlayerStore.CurrentPlayer.WonLast
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                OpponentCardsRevealed = Visibility.Collapsed;
                OpponentCardsNotRevealed = Visibility.Visible;
                IsCurrentPlayerWinner = Visibility.Collapsed;
            }

            PlayerActionButtonsEnabled = !string.IsNullOrEmpty(Table.ActivePlayerName)
                && Table.ActivePlayerName == PlayerStore.CurrentPlayer.Name;
            if (PlayerActionButtonsEnabled)
            {
                if (PlayerStore.CurrentPlayer.Chips > Table.ToCall * 2)
                {
                    RaiseMin = Table.ToCall * 2;
                    RaiseMax = PlayerStore.CurrentPlayer.Chips;
                    IsRaiseButtonEnabled = true;
                } else
                {
                    IsRaiseButtonEnabled = false;
                }

                if (Table.ToCall > PlayerStore.CurrentPlayer.Chips) CallButtonText = "All In";
                else if (Table.ToCall <= PlayerStore.CurrentPlayer.RoundBet) CallButtonText = "Check";
                else CallButtonText = $"Call ({Table.ToCall - PlayerStore.CurrentPlayer.RoundBet}$)";
            }
            if (PlayerStore.CurrentPlayer.Cards.Count == 2)
            {
                CurrentPlayerHandName = PlayerStore.CurrentPlayer.HandName;
            }
            else
            {
                CurrentPlayerHandName = "No hand";
            }
        }
        else
        {
            IsplayerOnOwnTable = Visibility.Collapsed;
            IsJoinButtonVisible = Visibility.Visible;
            IsLeaveButtonVisible = Visibility.Collapsed;
            IsRaiseButtonEnabled = false;
            PlayerActionButtonsEnabled = false;

            OpponentCardsRevealed = Visibility.Visible;
            OpponentCardsNotRevealed = Visibility.Collapsed;
            IsCurrentPlayerWinner = Visibility.Collapsed;
        }
    }

    private void RebuildOpponentPlayers(bool isAtThisTable)
    {
        var shouldShow = Table.Players
            .Where(p => !isAtThisTable || p.Name != PlayerStore!.CurrentPlayer!.Name)
            .ToList();

        for (int i = OpponentPlayers.Count - 1; i >= 0; i--)
        {
            if (!shouldShow.Contains(OpponentPlayers[i]))
                OpponentPlayers.RemoveAt(i);
        }

        foreach (var player in shouldShow)
        {
            if (!OpponentPlayers.Contains(player))
                OpponentPlayers.Add(player);
        }
    }

    public void CallButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPlayerAction(PlayerAction.Call, 0);
    }
    public void RaiseButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPlayerAction(PlayerAction.Raise, SelectedRaiseValue);
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

    public static String ConvertBlind(BlindEnum blind)
    {
        switch (blind)
        {
            case BlindEnum.BigBlind: return "BigBlind";
            case BlindEnum.SmallBlind: return "SmallBlind";
            case BlindEnum.NotBlind: return "";
            default: throw new ArgumentException();
        }
    }
}
