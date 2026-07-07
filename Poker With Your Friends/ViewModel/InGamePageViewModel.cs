using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using System;

namespace Poker_With_Your_Friends.ViewModel
{
    public partial class InGamePageViewModel : ObservableObject
    {
        public Table Table { get; set; }

        private DispatcherQueue _dispatcherQueue;
        public InGamePageViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            Client.CurrentPlayer.OnPlayerButtonsChanged += UpdatePlayerActionButtons;
        }
        public InGamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Subscribe here
            Client.OnTableJoined += JoinGameHandle;
            Client.OnTableLeft += LeaveGameHandle;

            // ... rest of your existing logic ...
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Unsubscribe here
            Client.OnTableJoined -= JoinGameHandle;
            Client.OnTableLeft -= LeaveGameHandle;
        }
        public void Initialize(Table table)
        {
            this.Table = table;
        }

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

        public void UpdatePlayerActionButtons(bool enabled)
        {
            if(_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    this.PlayerActionButtonsEnabled = enabled;
                });
            }
            else
            {
                this.PlayerActionButtonsEnabled = enabled;
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
