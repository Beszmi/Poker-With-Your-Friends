using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Media.Protection.PlayReady;

namespace Poker_With_Your_Friends.ViewModel
{
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
        public InGamePageViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }
        public void Initialize(Client client, Table table)
        {
            this.client = client;
            PlayerStore = client.PlayerStore;
            this.Table = table;

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

                PlayerActionButtonsEnabled = (Table.ActivePlayerName == PlayerStore.CurrentPlayer.Name);
            }
            else
            {
                IsplayerOnOwnTable = Visibility.Collapsed;
                IsJoinButtonVisible = Visibility.Visible;
                IsLeaveButtonVisible = Visibility.Collapsed;
                PlayerActionButtonsEnabled = false;
            }
        }

        public void CallButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                Table.PlayerActionTcs.SetResult(Table.PlayerAction.Call);
            }
        }
        public void RaiseButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                Table.PlayerActionTcs.SetResult(Table.PlayerAction.Raise);
            }
        }
        public void FoldButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                Table.PlayerActionTcs.SetResult(Table.PlayerAction.Fold);
            }
        }
    }
}
