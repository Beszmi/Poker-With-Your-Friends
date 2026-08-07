using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
    public partial String CurrentPlayerHandName { get; set; } = "";

    public ObservableCollection<PlayerSeatViewModel> PlayerSeats { get; } = new();

    [ObservableProperty]
    public partial Visibility CardsDealt { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility IsCurrentPlayerWinner { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility RevealCardsButtonVisible { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility ShowCardsButtonVisible { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial bool ShowCardsButtonEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool CallButtonEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool AllInButtonEnabled { get; set; } = false;

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
        DisableActionButtons();
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

        RebuildPlayerSeats(isAtThisTable);

        if (isAtThisTable)
        {
            IsplayerOnOwnTable = Visibility.Visible;
            IsJoinButtonVisible = Visibility.Collapsed;
            IsLeaveButtonVisible = Visibility.Visible;

            if (Table.HandOver)
            {
                IsCurrentPlayerWinner = PlayerStore.CurrentPlayer.WonLast
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                DisableActionButtons();
                RevealCardsButtonVisible = Visibility.Visible;
            }
            else
            {
                IsCurrentPlayerWinner = Visibility.Collapsed;
                RevealCardsButtonVisible = Visibility.Collapsed;
            }

            ShowCardsButtonVisible = !Table.HandOver && Table.IsShowdown && !PlayerStore.CurrentPlayer.HasFolded
                ? Visibility.Visible : Visibility.Collapsed;

            PlayerActionButtonsEnabled = !Table.HandOver
                && !string.IsNullOrEmpty(Table.ActivePlayerName)
                && Table.ActivePlayerName == PlayerStore.CurrentPlayer.Name;

            if (PlayerActionButtonsEnabled && Table.IsShowdown)
            {
                // Showdown: only Show or Fold are allowed.
                CallButtonEnabled = false;
                AllInButtonEnabled = false;
                IsRaiseButtonEnabled = false;
                ShowCardsButtonEnabled = true;
            }
            else if (PlayerActionButtonsEnabled)
            {
                ShowCardsButtonEnabled = false;
                int amountToCall = Math.Max(0, Table.ToCall - PlayerStore.CurrentPlayer.RoundBet);
                int chips = PlayerStore.CurrentPlayer.Chips;

                // Compare against chips needed this action, not raw ToCall (blinds already in RoundBet).
                CallButtonEnabled = amountToCall == 0 || chips > amountToCall;
                AllInButtonEnabled = chips > 0;

                if (CallButtonEnabled && chips > Table.ToCall * 2)
                {
                    RaiseMin = Table.ToCall * 2;
                    RaiseMax = chips;
                    IsRaiseButtonEnabled = true;
                }
                else
                {
                    IsRaiseButtonEnabled = false;
                }

                if (amountToCall == 0) CallButtonText = "Check";
                else CallButtonText = $"Call ({amountToCall}$)";
            }
            else if (!Table.HandOver)
            {
                // Not our turn — clear per-button flags (Fold uses PlayerActionButtonsEnabled).
                CallButtonEnabled = false;
                AllInButtonEnabled = false;
                IsRaiseButtonEnabled = false;
                ShowCardsButtonEnabled = false;
            }

            if (PlayerStore.CurrentPlayer.Cards.Count == 2)
            {
                CurrentPlayerHandName = PlayerStore.CurrentPlayer.HandName;
            }
            else
            {
                CurrentPlayerHandName = "";
            }
        }
        else
        {
            IsplayerOnOwnTable = Visibility.Collapsed;
            IsJoinButtonVisible = Visibility.Visible;
            IsLeaveButtonVisible = Visibility.Collapsed;
            RevealCardsButtonVisible = Visibility.Collapsed;
            ShowCardsButtonVisible = Visibility.Collapsed;
            DisableActionButtons();

            IsCurrentPlayerWinner = Visibility.Collapsed;
        }
    }

    private void DisableActionButtons()
    {
        PlayerActionButtonsEnabled = false;
        CallButtonEnabled = false;
        AllInButtonEnabled = false;
        IsRaiseButtonEnabled = false;
        ShowCardsButtonEnabled = false;
    }

    private void RebuildPlayerSeats(bool isAtThisTable)
    {
        var orderedPlayers = Table.Players.ToList();
        string? localPlayerName = PlayerStore?.CurrentPlayer?.Name;

        if (isAtThisTable && localPlayerName != null)
        {
            int localPlayerIndex = orderedPlayers.FindIndex(player => player.Name == localPlayerName);
            if (localPlayerIndex > 0)
            {
                orderedPlayers = orderedPlayers
                    .Skip(localPlayerIndex)
                    .Concat(orderedPlayers.Take(localPlayerIndex))
                    .ToList();
            }
        }

        bool useRevealedTemplate = !isAtThisTable || Table.HandOver || Table.IsShowdown;

        PlayerSeats.Clear();
        foreach (Player player in orderedPlayers)
        {
            bool isLocalPlayer = isAtThisTable && player.Name == localPlayerName;
            PlayerSeats.Add(new PlayerSeatViewModel(
                player,
                this,
                isLocalPlayer,
                useRevealedTemplate));
        }
    }

    [RelayCommand]
    private void Call()
    {
        SubmitPlayerAction(PlayerAction.Call, 0);
    }

    [RelayCommand]
    private void Raise()
    {
        SubmitPlayerAction(PlayerAction.Raise, SelectedRaiseValue);
    }

    [RelayCommand]
    private void Fold()
    {
        SubmitPlayerAction(PlayerAction.Fold, 0);
    }

    [RelayCommand]
    private void AllIn()
    {
        SubmitPlayerAction(PlayerAction.AllIn, 0);
    }

    [RelayCommand]
    private void ShowCards()
    {
        SubmitPlayerAction(PlayerAction.Show, 0);
    }

    [RelayCommand]
    private void RevealCards()
    {
        if (PlayerStore?.CurrentPlayer == null)
        {
            return;
        }

        client?.SendPlayerRevealCards(PlayerStore.CurrentPlayer.Name);
        RevealCardsButtonVisible = Visibility.Collapsed;
    }

    private void SubmitPlayerAction(PlayerAction action, int amount)
    {
        if (!PlayerActionButtonsEnabled || client == null)
        {
            return;
        }

        client.SendPlayerAction(action, amount);
        Timer?.StopTimer();
        DisableActionButtons();
    }

    // if player blind matches selected blind visible otherwise collapsed
    public static Visibility ConvertBlind(BlindEnum playerBlind, BlindEnum blind)
    {
        return playerBlind == blind ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Brush LastActionToColor(String action)
    {
        return action switch
        {
            "AllIn" => new SolidColorBrush(Colors.Red),
            "Call" => new SolidColorBrush(Colors.Green),
            "Fold" => new SolidColorBrush(Colors.Gray),
            "Raise" => new SolidColorBrush(Colors.Blue),
            "Check" => new SolidColorBrush(Colors.Green),
            "" => new SolidColorBrush(Colors.Transparent),
            _ => throw new ArgumentException("Wrong last action"),
        };
    }

    public static Visibility LastActionVisibility(String action)
    {
        return action == "" ?  Visibility.Collapsed: Visibility.Visible;
    }
}
