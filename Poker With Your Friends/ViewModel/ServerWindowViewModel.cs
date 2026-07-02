using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;

namespace Poker_With_Your_Friends.ViewModel
{
    internal partial class ServerWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPlayerChips))]
        public object? selectedPlayerName;

        [ObservableProperty]
        public String? newName;

        [ObservableProperty]
        public int? newChips;

        private Server server;

        public Game game = Game.Instance;

        public ServerWindowViewModel(Server server)
        {
            this.server = server;
            game.ReadPlayersFromXml(Game.PlayerfilePath);
        }

        public int SelectedPlayerChips
        {
            get
            {
                string? name = SelectedPlayerName as string;
                if (string.IsNullOrEmpty(name)) return 0;

                var player = game.GetPlayerFromName(name);
                return player != null ? player.Chips : 0;
            }
        }

        public void DeletePlayer()
        {
            string? name = SelectedPlayerName as string;
            if (name != null)
            {
                game.RemovePlayer(game.GetPlayerFromName(name));
                SelectedPlayerName = null;
            }
        }

        public void EditPlayer()
        {
            string? name = SelectedPlayerName as string;
            if (name == null) return;

            var player = game.GetPlayerFromName(name);
            bool chipsChanged = false;

            if (NewChips != null && NewChips != player.Chips)
            {
                player.Chips = (int)NewChips;
                chipsChanged = true;
            }

            if (!string.IsNullOrEmpty(NewName) && NewName != name)
            {
                player.Name = NewName;
                game.RefreshPlayerNames();

                SelectedPlayerName = NewName;
            }
            else if (chipsChanged)
            {
                OnPropertyChanged(nameof(SelectedPlayerChips));
            }
        }
        
        public void StopServer()
        {
            server?.Stop();
        }

        public void Window_Closed(object sender, WindowEventArgs args)
        {
            game.SavePlayersToXml(Game.PlayerfilePath);
        }
    }
}
