using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Poker_With_Your_Friends.ViewModel;

public sealed class AddTableMenuItem
{
    public AddTableMenuItem(GameMenuPageViewModel owner)
    {
        Owner = owner;
    }

    public GameMenuPageViewModel Owner { get; }
}

public partial class GameMenuPageViewModel : ObservableObject
{
    public Client client;
    public IPlayerStore PlayerStore { get; private set; }

    private Game game;
    public ObservableCollection<Table> Tables { get; set; }
    public ObservableCollection<object> TableMenuItems { get; } = new();
    public AddTableMenuItem AddTableItem { get; }
    
    public static Action<String> GameMenuError;

    [ObservableProperty]
    public partial String? NewTableName { get; set; }

    [ObservableProperty]
    public partial bool IsNewTableButtonEnabled { get; set; } = true;

    [ObservableProperty]
    public partial String FileLocation { get; set; } = "";

    [ObservableProperty]
    public partial bool SuccessfullyPickedFile { get; set; } = false;

    public static event Action<String>? FileSelected;

    public GameMenuPageViewModel()
    {
        game = Game.ClientInstance;
        Tables = game.Tables;
        AddTableItem = new AddTableMenuItem(this);
        Tables.CollectionChanged += Tables_CollectionChanged;
        RebuildTableMenuItems();

        GoToPage2Command = new RelayCommand(() =>
        {
            NavigationRequested?.Invoke(typeof(InGamePage), null);
        });
    }

    private void Tables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTableMenuItems();
    }

    private void RebuildTableMenuItems()
    {
        TableMenuItems.Clear();

        foreach (Table table in Tables)
        {
            TableMenuItems.Add(table);
        }

        TableMenuItems.Add(AddTableItem);
    }

    public void Initialize(Client c)
    {
        client = c;
        PlayerStore = c.PlayerStore;
    }

    public async Task CreateNewTableAsync()
    {
        if (!IsNewTableButtonEnabled)
        {
            return;
        }

        string? tableName = NewTableName?.Trim();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            if (game.IsTableNameTaken(tableName))
            {
                GameMenuError?.Invoke("Table with this name already exists!");
                return;
            }
            IsNewTableButtonEnabled = false;

            var tcs = new TaskCompletionSource<Table>();

            void CheckNewTable(Table t)
            {
                if (t.Name == tableName)
                {
                    tcs.TrySetResult(t);
                }
            }

            game.OnTableAdded += CheckNewTable;

            client.CreateNewTable(tableName);

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
                NewTableName = string.Empty;
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

    public async Task SelectFileAsync()
    {
        var picker = new FileOpenPicker();

        var gameWindow = App.GameWindowInstance
            ?? throw new InvalidOperationException("GameWindow is not open.");

        var hwnd = WindowNative.GetWindowHandle(gameWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".jpg");

        StorageFile? file = await picker.PickSingleFileAsync();

        SuccessfullyPickedFile = false;

        if (file is null)
        {
            FileLocation = "No file picked";
            return;
        }

        if (!string.Equals(file.FileType, ".jpg", StringComparison.OrdinalIgnoreCase))
        {
            FileLocation = "Wrong file format";
            return;
        }

        try
        {
            long length = new System.IO.FileInfo(file.Path).Length;
            if (length > 8388608)
            {
                FileLocation = "File too big";
                return;
            }
        }
        catch (Exception ex)
        {
            GameMenuError?.Invoke(ex.Message);
            FileLocation = "Could not read file";
            return;
        }

        if (!await Utils.IsSafeJpegAsync(file))
        {
            FileLocation = "Jpg not safe";
            return;
        }

        FileLocation = $"Picked: {file.Path}";
        SuccessfullyPickedFile = true;

        FileSelected?.Invoke(file.Path);
    }
}
