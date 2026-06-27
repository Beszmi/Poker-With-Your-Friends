using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.ViewModel
{
    public class MainWindowViewModel
    {
        public void StartGameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            GameWindow newWindow = new GameWindow();
            newWindow.Activate();
        }

        public void StartServerClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ServerWindow newWindow = new ServerWindow();
            newWindow.Activate();
        }
    }
}
