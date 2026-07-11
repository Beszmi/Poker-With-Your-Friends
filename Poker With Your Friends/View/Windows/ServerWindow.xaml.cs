using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System.ComponentModel;

namespace Poker_With_Your_Friends       
{
    public sealed partial class ServerWindow : Window, INotifyPropertyChanged
    {
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            viewModel.game.SavePlayersToXml(Game.PlayerfilePath);
            viewModel.StopServer();
        }

        private ServerWindowViewModel viewModel;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private Visibility editPanelVisible = Visibility.Collapsed;

        public Visibility EditPanelVisible
        {
            get
            {
                return editPanelVisible;
            }
            set
            {
                if (editPanelVisible != value)
                {
                    editPanelVisible = value;
                    OnPropertyChanged(nameof(EditPanelVisible));
                }
            }
        }

        public ServerWindow(Server server)
        {
            viewModel = new ServerWindowViewModel(server);
            InitializeComponent();
        }

        public void DeletePlayerButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.DeletePlayer();
        }

        public void EditPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            EditPanelVisible = Visibility.Visible;
        }

        public void EditDoneButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.EditPlayer();
            EditPanelVisible = Visibility.Collapsed;
        }
        public void DebugMode_Click(object sender, RoutedEventArgs e)
        {
            viewModel.FlipDebug();
        }
    }
}
