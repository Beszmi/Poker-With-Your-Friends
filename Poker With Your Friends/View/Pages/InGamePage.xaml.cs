using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends
{
    public sealed partial class InGamePage : Page
    {
        public static Action<Table>? OnJoinGameClick;
        public static Action<Table>? OnLeaveGameClick;

        private InGamePageViewModel viewModel = new InGamePageViewModel();
        public InGamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Unsubscribe here
            Client.OnTableJoined -= JoinGameHandle;
            Client.OnTableLeft -= LeaveGameHandle;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Table tableToDisplay)
            {
                // This is the table the user is CURRENTLY LOOKING AT.
                // It could be different from Client.CurrentTable if they are spectating!

                // You can pass this to your InGamePageViewModel to load the correct UI data
                Client.OnTableJoined += JoinGameHandle;
                Client.OnTableLeft += LeaveGameHandle;

                viewModel.Initialize(tableToDisplay);
                if (Client.CurrentTable == viewModel.Table)
                {
                    viewModel.IsplayerOnOwnTable = Visibility.Visible;
                }
                else
                {
                    viewModel.IsplayerOnOwnTable = Visibility.Collapsed;
                }
            }
        }

        private void LeaveTableButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack(new DrillInNavigationTransitionInfo());
        }
        private void LeaveGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (Client.CurrentTable != null && Client.CurrentTable == viewModel.Table)
            {
                viewModel.LeaveTableButtonEnabled = false;
                OnLeaveGameClick?.Invoke(viewModel.Table);
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (Client.CurrentTable != null)
            {
                DisplayErrorDialog("You are already in a game. Please leave your current game before joining a new one.");
                return;
            }
            viewModel.LeaveTableButtonEnabled = false;
            OnJoinGameClick?.Invoke(viewModel.Table);
        }

        private async void DisplayErrorDialog(String message)
        {
            ContentDialog myDialog = new ContentDialog();

            myDialog.XamlRoot = this.XamlRoot;

            myDialog.Title = "Error";
            myDialog.Content = message;
            myDialog.PrimaryButtonText = "Ok";

            ContentDialogResult result = await myDialog.ShowAsync();
        }

        private void JoinGameHandle()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Client.CurrentTable = viewModel.Table;

                    viewModel.IsplayerOnOwnTable = Visibility.Visible;
                    viewModel.IsJoinButtonVisible = Visibility.Collapsed;
                    viewModel.IsLeaveButtonVisible = Visibility.Visible;
                }
                catch (InvalidOperationException ex)
                {
                    DisplayErrorDialog(ex.Message);
                }
                viewModel.LeaveTableButtonEnabled = true;
            });
        }

        private void LeaveGameHandle()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Client.CurrentTable.RemovePlayer(Client.CurrentPlayer);
                Client.CurrentTable = null;

                viewModel.IsplayerOnOwnTable = Visibility.Collapsed;
                viewModel.IsJoinButtonVisible = Visibility.Visible;
                viewModel.IsLeaveButtonVisible = Visibility.Collapsed;
                viewModel.LeaveTableButtonEnabled = true;
            });
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Client.OnTableJoined -= JoinGameHandle;
            Client.OnTableLeft -= LeaveGameHandle;
        }
    }
}
