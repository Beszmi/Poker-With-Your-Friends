using Poker_With_Your_Friends.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.ViewModel
{
    class GameWindowViewModel
    {
        public String? NewTableName { get; set; }
        public ObservableCollection<Table> Tables { get; set; } = Game.Tables;

        public void NewgameButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (NewTableName != null)
            {
                Game.AddTable(NewTableName);
            } else
            {
                // Handle the case where NewTableName is null (e.g., show an error message)
            }
        }

        public void ItemClicked(object sender, Microsoft.UI.Xaml.Controls.ItemClickEventArgs e)
        {
            if (e.ClickedItem is Table table)
            {
                // Handle the item click event here
                // For example, you can navigate to a new page or perform some action with the clicked table
            }
        }
    }
}
