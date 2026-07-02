using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.ViewModel
{
    internal partial class ServerWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPlayerChips))]
        public String? selectedPlayerName;

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
                if (string.IsNullOrEmpty(SelectedPlayerName))
                    return 0;

                var player = game.GetPlayerFromName(SelectedPlayerName);
                return player != null ? player.Chips : 0;
            }
        }

        public void DeletePlayer()
        {
            if (SelectedPlayerName != null)
            {
                game.RemovePlayer(game.GetPlayerFromName(SelectedPlayerName));
                SelectedPlayerName = null;
            }
        }

        public void EditPlayer()
        {
            var player = game.GetPlayerFromName(SelectedPlayerName);
            if (player == null) return;

            bool chipsChanged = false;

            if (NewChips != null && NewChips != player.Chips)
            {
                player.Chips = (int)NewChips;
                chipsChanged = true;
            }

            if (!string.IsNullOrEmpty(NewName) && NewName != SelectedPlayerName)
            {
                game.RefreshPlayerNames();

                player.Name = NewName;
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
