using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends;

public sealed partial class GameWindow : Window
{
    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (App.GameWindowInstance == this)
        {
            App.GameWindowInstance = null;
        }

        if (client != null)
        {
            client.OnErrorReceived -= OnServerErrorReceived;
            client.OnLocalError -= OnLocalErrorReceived;
        }
    }

    private Client? client;

    private GameWindowViewModel viewModel = new GameWindowViewModel();

    public Frame Frame { get; private set; } = new Frame();
    public GameWindow(Client client)
    {
        InitializeComponent();

        App.GameWindowInstance = this;
        this.client = client;

        client.OnErrorReceived += OnServerErrorReceived;
        client.OnLocalError += OnLocalErrorReceived;

        RootFrame.Navigate(typeof(GameMenuPage), this.client);
    }

    private void OnServerErrorReceived(string errorMessage)
    {
        DispatcherQueue.TryEnqueue(() => DisplayServerErrorDialog(errorMessage));
    }

    private void OnLocalErrorReceived(string errorMessage)
    {
        DispatcherQueue.TryEnqueue(() => DisplayErrorDialog(errorMessage));
    }

    public async void DisplayErrorDialog(string message)
    {
        ContentDialog myDialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Local Error",
            Content = message,
            PrimaryButtonText = "Ok"
        };

        await myDialog.ShowAsync();
    }

    public async void DisplayServerErrorDialog(string message)
    {
        ContentDialog myDialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Error Recieved from server",
            Content = message,
            PrimaryButtonText = "Ok"
        };

        await myDialog.ShowAsync();
    }

    public void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
    }
}
