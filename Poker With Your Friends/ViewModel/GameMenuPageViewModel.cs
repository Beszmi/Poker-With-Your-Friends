using CommunityToolkit.Mvvm.Input;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Poker_With_Your_Friends.ViewModel
{
    internal class GameMenuPageViewModel
    {
        public GameMenuPageViewModel()
        {
            game = Game.Instance;
            Tables = game.Tables;

            GoToPage2Command = new RelayCommand(() =>
            {
                NavigationRequested?.Invoke(typeof(InGamePage), null);
            });
        }

        // Add a new table with a name
        public String? NewTableName { get; set; }
        private Game game;
        public ObservableCollection<Table> Tables { get; set; }

        public void CreateNewTable()
        {
            if (!string.IsNullOrWhiteSpace(NewTableName))
            {
                game.AddTable(NewTableName);
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
