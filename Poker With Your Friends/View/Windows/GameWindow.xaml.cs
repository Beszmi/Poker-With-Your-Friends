using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GameWindow : Window
    {
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            client.Disconnect();
        }
        private Client client;

        private GameWindowViewModel viewModel = new GameWindowViewModel();

        public Frame Frame { get; private set; }
        public GameWindow()
        {
            InitializeComponent();

            RootFrame.Navigate(typeof(GameMenuPage));
        }

        public GameWindow(Client client)
        {
            InitializeComponent();
            this.client = client;

            RootFrame.Navigate(typeof(GameMenuPage));
        }

        public void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
