using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel = new MainWindowViewModel();
        public MainWindow()
        {
            InitializeComponent();

            App.MainDispatcher = this.DispatcherQueue;

            viewModel.OnServerConnected += (Client c) =>
            {
                SetUpServerErrorHandler(c);
            };
        }
        public async void DisplayErrorDialog(String message)
        {
            ContentDialog myDialog = new ContentDialog();

            myDialog.XamlRoot = this.Content.XamlRoot;

            myDialog.Title = "Local Error";
            myDialog.Content = message;
            myDialog.PrimaryButtonText = "Ok";

            ContentDialogResult result = await myDialog.ShowAsync();
        }

        public async void DisplayServerErrorDialog(String message)
        {
            ContentDialog myDialog = new ContentDialog();

            myDialog.XamlRoot = this.Content.XamlRoot;

            myDialog.Title = "Error Recieved from server";
            myDialog.Content = message;
            myDialog.PrimaryButtonText = "Ok";

            ContentDialogResult result = await myDialog.ShowAsync();
        }

        public void SetUpServerErrorHandler(Client c)
        {
            if (c != null)
            {
                c.OnErrorReceived += (errorMessage) =>
                {
                    App.MainDispatcher.TryEnqueue(() =>
                    {
                        DisplayServerErrorDialog(errorMessage);
                    });
                };
            }
        }
    }
}
