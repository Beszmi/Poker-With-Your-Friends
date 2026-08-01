using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;
using Windows.System;

namespace Poker_With_Your_Friends;

public sealed partial class GameMenuPage : Page
{
    private readonly GameMenuPageViewModel viewModel;

    public GameMenuPage()
    {
        viewModel = App.Current.Services.GetRequiredService<GameMenuPageViewModel>();
        this.InitializeComponent();
        this.DataContext = viewModel;

        viewModel.NavigationRequested += (targetPageType, parameter) =>
        {
            this.Frame.Navigate(targetPageType, parameter, new DrillInNavigationTransitionInfo());
        };
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        GameMenuPageViewModel.GameMenuError -= DisplayErrorDialog;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        GameMenuPageViewModel.GameMenuError -= DisplayErrorDialog;
        GameMenuPageViewModel.GameMenuError += DisplayErrorDialog;

        if (e.Parameter is Client c)
        {
            viewModel.Initialize(c);
        }
    }
    private void Table_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Table clickedTable)
        {
            viewModel.ViewTable(clickedTable);
        }
    }

    private async void CreateTableButton_Click(object sender, RoutedEventArgs e)
    {
        await viewModel.CreateNewTableAsync();
    }

    private async void NewTableNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        e.Handled = true;
        await viewModel.CreateNewTableAsync();
    }

    private async void DisplayErrorDialog(string message)
    {
        ContentDialog myDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Local Error",
            Content = message,
            PrimaryButtonText = "Ok"
        };

        await myDialog.ShowAsync();
    }

    private void PickFileButton_Click(object sender, RoutedEventArgs e)
    {
        _ = viewModel.SelectFileAsync();
    }
}
