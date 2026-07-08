using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Poker_With_Your_Friends.Model
{
    public partial class PlayerStore : ObservableObject, IPlayerStore
    {
        [ObservableProperty]
        private Player? currentPlayer;

        [ObservableProperty]
        private Table? currentTable;
    }
}
