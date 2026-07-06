using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends
{
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
                LocalErrorHandler();
            };

            viewModel.OnClientError += (String msg) =>
            {
                DisplayErrorDialog(msg);
            };
            GameMenuPageViewModel.GameMenuError += DisplayErrorDialog;
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

        public void LocalErrorHandler()
        {
            viewModel.client.OnLocalError += (errorMessage) =>
            {
                App.MainDispatcher.TryEnqueue(() =>
                {
                    DisplayServerErrorDialog(errorMessage);
                });
            };
        }
    }
}
