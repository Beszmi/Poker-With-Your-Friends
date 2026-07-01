using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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
        public void Initialize(Model.Table table)
        {
            this.Table = table;
        }

        [ObservableProperty]
        public Visibility isJoinButtonVisible;

        [ObservableProperty]
        public Visibility isLeaveButtonVisible;

        [ObservableProperty]
        public Visibility isplayerOnOwnTable;

        [ObservableProperty]
        public String? tableText = "Empty text";

        [ObservableProperty]
        public bool playerActionButtonsEnabled = false;

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
