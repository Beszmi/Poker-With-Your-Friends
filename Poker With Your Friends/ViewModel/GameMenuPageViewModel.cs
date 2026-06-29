using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Poker_With_Your_Friends.ViewModel
{
    internal class GameMenuPageViewModel
    {
        public GameMenuPageViewModel()
        {
            GoToPage2Command = new RelayCommand(() =>
            {
                NavigationRequested?.Invoke(typeof(InGamePage), null);
            });
        }

        // Add a new table with a name
        public String? NewTableName { get; set; }
        public ObservableCollection<Table> Tables { get; set; } = Game.Tables;

        public void CreateNewTable()
        {
            if (!string.IsNullOrWhiteSpace(NewTableName))
            {
                Game.AddTable(NewTableName);
            }
            else
            {
                // Handle the case where NewTableName is null (e.g., show an error message)
            }
        }

        //Navigation event to notify the view when navigation is requested
        public Action<Type, object?>? NavigationRequested;

        public ICommand GoToPage2Command { get; }

        public void ViewTable(Table table)
        {
            NavigationRequested?.Invoke(typeof(InGamePage), table);
        }
    }
}
