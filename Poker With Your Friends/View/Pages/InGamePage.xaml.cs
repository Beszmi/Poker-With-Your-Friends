using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;
using System.Linq;

namespace Poker_With_Your_Friends
{
    public sealed partial class InGamePage : Page
    {
        public static Action<Table>? OnJoinGameClick;
        public static Action<Table>? OnLeaveGameClick;

        public InGamePageViewModel viewModel { get; } = new InGamePageViewModel();
        private Client client;

        public InGamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Unsubscribe here
            client.OnTableJoined -= JoinGameHandle;
            client.OnTableLeft -= LeaveGameHandle;
            client.OnTableUpdated -= viewModel.NetworkTableUpdated;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is object[] args && args.Length == 2)
            {
                // This is the table the user is CURRENTLY LOOKING AT.

                client = args[0] as Client;
                Table tableToDisplay = args[1] as Table;

                client.OnTableJoined += JoinGameHandle;
                client.OnTableLeft += LeaveGameHandle;
                client.OnTableUpdated += viewModel.NetworkTableUpdated;

                viewModel.Initialize(client, tableToDisplay);
                if (viewModel.PlayerStore.CurrentTable == viewModel.Table)
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
            if (viewModel.PlayerStore.CurrentPlayer != null && viewModel.PlayerStore.CurrentTable == viewModel.Table)
            {
                viewModel.LeaveTableButtonEnabled = false;
                OnLeaveGameClick?.Invoke(viewModel.Table);
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            bool isAlreadyPlayingSomewhere = Game.ClientInstance.Tables.Any(t => t.Players.Any(p => p.Name == viewModel.PlayerStore?.CurrentPlayer?.Name));
            if (isAlreadyPlayingSomewhere)
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
                    viewModel.PlayerStore.CurrentTable = viewModel.Table;
                    viewModel.RefreshLocalState();
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
                viewModel.PlayerStore.CurrentTable?.RemovePlayer(viewModel.PlayerStore.CurrentPlayer);
                viewModel.PlayerStore.CurrentTable = null;

                viewModel.IsplayerOnOwnTable = Visibility.Collapsed;
                viewModel.IsJoinButtonVisible = Visibility.Visible;
                viewModel.IsLeaveButtonVisible = Visibility.Collapsed;
                viewModel.LeaveTableButtonEnabled = true;
            });
        }
    }
}
