using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Linq;
using Windows.Media.Protection.PlayReady;
using static Poker_With_Your_Friends.Model.Table;
using static System.Collections.Specialized.BitVector32;

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

        public static event Action<Table.PlayerAction, int>? OnSendPlayerAction;

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
            TableText = Table.Name + " Active: " + Table.IsGameActive + "Current player: " + Table.ActivePlayerName;
        }

        public void CallButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                OnSendPlayerAction?.Invoke(PlayerAction.Call, 0);
                //Table.SubmitPlayerAction(PlayerStore.CurrentPlayer, PlayerAction.Call, 0);
            }
        }
        public void RaiseButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                OnSendPlayerAction?.Invoke(PlayerAction.Raise, 10);
                //Table.SubmitPlayerAction(PlayerStore.CurrentPlayer, PlayerAction.Raise, 10);
            }
        }
        public void FoldButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerActionButtonsEnabled)
            {
                OnSendPlayerAction?.Invoke(PlayerAction.Fold, 0);
                //Table.SubmitPlayerAction(PlayerStore.CurrentPlayer, PlayerAction.Fold, 0);
            }
        }
    }
}
