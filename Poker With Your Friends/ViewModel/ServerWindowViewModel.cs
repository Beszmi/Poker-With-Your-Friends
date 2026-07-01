using CommunityToolkit.Mvvm.ComponentModel;
using Poker_With_Your_Friends.Model;
using System;

namespace Poker_With_Your_Friends.ViewModel
{
    internal partial class ServerWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPlayerChips))]
        public String? selectedPlayerName;

        [ObservableProperty]
        public String? newName;

        public int SelectedPlayerChips
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedPlayerName))
                    return 0;

                var player = Game.GetPlayerFromName(SelectedPlayerName);
                return player != null ? player.Chips : 0;
            }
        }

        [ObservableProperty]
        public int? newChips;

        public void DeletePlayer()
        {
            if (SelectedPlayerName != null)
            {
                Game.RemovePlayer(Game.GetPlayerFromName(SelectedPlayerName));
                SelectedPlayerName = null;
            }
        }

        public void EditPlayer()
        {
            var player = Game.GetPlayerFromName(SelectedPlayerName);
            if (player == null) return;

            bool chipsChanged = false;

            if (NewChips != null && NewChips != player.Chips)
            {
                player.Chips = (int)NewChips;
                chipsChanged = true;
            }

            if (!string.IsNullOrEmpty(NewName) && NewName != SelectedPlayerName)
            {
                Game.RefreshPlayerNames();

                player.Name = NewName;
                SelectedPlayerName = NewName;
            }
            else if (chipsChanged)
            {
                OnPropertyChanged(nameof(SelectedPlayerChips));
            }
        }
    }
}
