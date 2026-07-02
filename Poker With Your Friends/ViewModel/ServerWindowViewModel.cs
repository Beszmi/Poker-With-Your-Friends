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
            ReadPlayersFromXml(Game.PlayerfilePath);
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

        public void ReadPlayersFromXml(String xmlFilePath)
        {
            if (!File.Exists(xmlFilePath))
                return;

            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));
            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open))
            {
                ObservableCollection<Player>? deserializedList = serializer.Deserialize(fileStream) as ObservableCollection<Player>;
                if (deserializedList != null)
                {
                    game.Players.Clear();
                    foreach (var player in deserializedList)
                    {
                        game.AddPlayer(player);
                    }
                }
            }
        }
        public void SavePlayersToXml(String xmlFilePath)
        {
            if (!Directory.Exists(Game.PlayerfolderPath))
            {
                Directory.CreateDirectory(Game.PlayerfolderPath);
            }

            if (game.Players == null || game.Players.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"SAVE FAILED: Players empty");
                return;
            }
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Player>));
                using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Create))
                {
                    serializer.Serialize(fileStream, game.Players);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SAVE FAILED: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"INNER EXCEPTION: {ex.InnerException.Message}");
                }
            }


        }
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            SavePlayersToXml(Game.PlayerfilePath);
        }
    }
}
