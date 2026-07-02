using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Poker_With_Your_Friends
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GameMenuPage : Page
    {
        private GameMenuPageViewModel viewModel = new GameMenuPageViewModel();
        public GameMenuPage()
        {
            this.InitializeComponent();

            this.DataContext = viewModel;

            viewModel.NavigationRequested += (targetPageType, parameter) =>
            {
                this.Frame.Navigate(targetPageType, parameter, new DrillInNavigationTransitionInfo());
            };
        }
        private void Table_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Table clickedTable)
            {
                viewModel.ViewTable(clickedTable);
            }
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.CreateNewTable();
        }
    }
}
