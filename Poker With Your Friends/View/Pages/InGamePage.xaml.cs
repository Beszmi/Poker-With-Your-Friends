using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Poker_With_Your_Friends
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InGamePage : Page
    {
        private InGamePageViewModel viewModel = new InGamePageViewModel();
        public InGamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Table tableToDisplay)
            {
                // This is the table the user is CURRENTLY LOOKING AT.
                // It could be different from Client.CurrentTable if they are spectating!

                // You can pass this to your InGamePageViewModel to load the correct UI data
                viewModel.Initialize(tableToDisplay);
            }
        }

        private void LeaveTableButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack(new DrillInNavigationTransitionInfo());
        }
        private void LeaveGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (Client.CurrentTable != null)
            {
                Client.CurrentTable.RemovePlayer(Client.CurrentPlayer);
                Client.CurrentTable = null;
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                viewModel.Table.AddPlayer(Client.CurrentPlayer);
                Client.CurrentTable = viewModel.Table;
            }
            catch (InvalidOperationException ex)
            {
                DisplayTableFullDialog(ex.Message);
            }
        }

        private async void DisplayTableFullDialog(String message)
        {
            ContentDialog myDialog = new ContentDialog();

            myDialog.XamlRoot = this.XamlRoot;

            myDialog.Title = "Error";
            myDialog.Content = message;
            myDialog.PrimaryButtonText = "Ok";

            ContentDialogResult result = await myDialog.ShowAsync();
        }
    }
}
