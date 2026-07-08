using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends
{
    public sealed partial class GameWindow : Window
    {
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            client?.Disconnect();
        }
        private Client? client;

        private GameWindowViewModel viewModel = new GameWindowViewModel();

        public Frame Frame { get; private set; } = new Frame();
        public GameWindow(Client client)
        {
            InitializeComponent();

            this.client = client;

            RootFrame.Navigate(typeof(GameMenuPage), this.client);
        }

        public void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
