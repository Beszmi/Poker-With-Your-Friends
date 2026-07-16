using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel viewModel = new MainWindowViewModel();
    private Action<string>? _serverErrorHandler;

    public MainWindow()
    {
        InitializeComponent();

        App.MainDispatcher = this.DispatcherQueue;

        viewModel.OnServerConnected += (Client c) =>
        {
            SetUpServerErrorHandler(c);
        };

        viewModel.OnGameWindowOpening += DetachClientErrorHandlers;

        viewModel.OnClientError += (String msg) =>
        {
            DisplayErrorDialog(msg);
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
        DetachClientErrorHandlers();

        if (c == null)
        {
            return;
        }

        _serverErrorHandler = (errorMessage) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DisplayServerErrorDialog(errorMessage);
            });
        };

        c.OnErrorReceived += _serverErrorHandler;
    }

    private void DetachClientErrorHandlers()
    {
        if (viewModel.client != null && _serverErrorHandler != null)
        {
            viewModel.client.OnErrorReceived -= _serverErrorHandler;
        }

        _serverErrorHandler = null;
    }

    private void LeaveServer_Click(object sender, RoutedEventArgs e)
    {
        viewModel.LeaveServer();
    }
}
