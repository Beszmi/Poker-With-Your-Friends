using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Poker_With_Your_Friends       
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ServerWindow : Window, INotifyPropertyChanged
    {
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            viewModel.StopServer();
            viewModel.game.SavePlayersToXml(Game.PlayerfilePath);
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
    }
}
