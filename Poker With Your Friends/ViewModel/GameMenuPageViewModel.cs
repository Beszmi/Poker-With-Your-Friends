using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Poker_With_Your_Friends.ViewModel;

internal partial class GameMenuPageViewModel : ObservableObject
{
    public Client client;
    public IPlayerStore PlayerStore { get; private set; }

    private Game game;
    public ObservableCollection<Table> Tables { get; set; }
    
    public static Action<String> GameMenuError;

    public String? NewTableName { get; set; }

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
