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
        public Client client;
        public IPlayerStore PlayerStore { get; private set; }

        private Game game;
        public ObservableCollection<Table> Tables { get; set; }
        
        public static Action<String> GameMenuError;

        public String? NewTableName { get; set; }

        [ObservableProperty]
        public bool isNewTableButtonEnabled = true;

        public GameMenuPageViewModel()
        {
            game = Game.ClientInstance;
            Tables = game.Tables;

            GoToPage2Command = new RelayCommand(() =>
            {
                NavigationRequested?.Invoke(typeof(InGamePage), null);
            });
        }

        public void Initialize(Client c)
        {
            client = c;
            PlayerStore = c.PlayerStore;
        }

        public async Task CreateNewTableAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewTableName))
            {
                if (game.IsTableNameTaken(NewTableName))
                {
                    GameMenuError?.Invoke("Table with this name already exists!");
                    return;
                }
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

                client.CreateNewTable(NewTableName);

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
            NavigationRequested?.Invoke(typeof(InGamePage), new object[] { client, table });
        }
    }
}
