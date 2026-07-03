using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Poker_With_Your_Friends.ViewModel
{
    internal partial class GameMenuPageViewModel : ObservableObject
    {
        private Client client;
        public GameMenuPageViewModel()
        {
            game = Game.Instance;
            Tables = game.Tables;

            GoToPage2Command = new RelayCommand(() =>
            {
                NavigationRequested?.Invoke(typeof(InGamePage), null);
            });
        }

        public void SetClient(Client c)
        {
            client = c;
        }

        // Add a new table with a name
        public String? NewTableName { get; set; }
        private Game game;
        public ObservableCollection<Table> Tables { get; set; }

        [ObservableProperty]
        public bool isNewTableButtonEnabled = true;

        public async Task CreateNewTableAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewTableName))
            {
                IsNewTableButtonEnabled = false;

                var tcs = new TaskCompletionSource<Table>();

                void CheckNewTable(Table t)
                {
                    if (t.Name == NewTableName)
                    {
                        tcs.TrySetResult(t);
                    }
                }

                game.OnTableAdded += CheckNewTable;

                client?.CreateNewTable(NewTableName);

                try
                {
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        System.Diagnostics.Debug.WriteLine("Adding new table failed. Server did not respond.");
                        return;
                    }

                    Table newlyCreatedTable = await tcs.Task;
                }
                finally
                {
                    game.OnTableAdded -= CheckNewTable;
                    IsNewTableButtonEnabled = true;
                }
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
