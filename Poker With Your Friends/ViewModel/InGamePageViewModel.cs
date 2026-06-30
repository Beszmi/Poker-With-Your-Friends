using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.ViewModel
{
    public partial class InGamePageViewModel : ObservableObject
    {
        public Table Table { get; set; }
        public void Initialize(Model.Table table)
        {
            this.Table = table;
        }

        [ObservableProperty]
        public Visibility isJoinButtonVisible;

        [ObservableProperty]
        public Visibility isLeaveButtonVisible;
    }
}
