using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends;

public sealed partial class GameMenuPage : Page
{
    private GameMenuPageViewModel viewModel = new GameMenuPageViewModel();
    public GameMenuPage()
    {
        this.InitializeComponent();

        viewModel = App.Current.Services.GetRequiredService<GameMenuPageViewModel>();
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

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        _ =viewModel.CreateNewTableAsync();
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
