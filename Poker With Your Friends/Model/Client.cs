using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    public class Client
    {
        public IPlayerStore PlayerStore { get; }

        public event Action<String>? OnSendPlayerAction;
        public event Action<String> OnErrorReceived;
        public event Action<String> OnLocalError;
        public event Action? OnTableUpdated;
        public event Action? OnTableJoined;
        public event Action? OnTableLeft;
        public String Host { get; set; }
        public int Port { get; set; }

        private PipeWriter? _writer;
        private TcpClient? _tcpClient;
        private CancellationTokenSource _cts = new();

        private Game game = Game.ClientInstance;

        public Client(String host, int port, IPlayerStore playerStore)
        {
            Host = host;
            Port = port;
            this.PlayerStore = playerStore;

            InGamePage.OnJoinGameClick += PlayerJoiningTable;
            InGamePage.OnLeaveGameClick += PlayerLeavingTable;

            OnSendPlayerAction = (actionStr) =>
            {
                if (game.Tables.IndexOf(PlayerStore.CurrentTable) != -1)
                {
                    SendMessage($"54{game.Tables.IndexOf(PlayerStore.CurrentTable)},{actionStr}");
                }
            };
        }

        public async Task ConnectAndRunAsync()
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(Host, Port);
            System.Diagnostics.Debug.WriteLine("Connected to server!");

            var stream = _tcpClient.GetStream();
            var reader = PipeReader.Create(stream);
            _writer = PipeWriter.Create(stream);

            // Run the receive loop in the background indefinitely
            _ = ReceiveLoopAsync(reader, _cts.Token);
        }

        private async Task ReceiveLoopAsync(PipeReader reader, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(token);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryReadMessage(ref buffer, out string? response))
                    {
                        InterpretMessage(response);

                        System.Diagnostics.Debug.WriteLine($"[Game Update]: {response}");
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection lost: {ex.Message}");
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out string? message)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n');
            if (position == null)
            {
                message = null;
                return false;
            }
            message = Encoding.UTF8.GetString(buffer.Slice(0, position.Value));
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        /* --------------------------------------------------------
         * 
         * Sending network data
         *
         -------------------------------------------------------*/
        private void SendMessage(string message)
        {
            if (_writer == null) return;
            byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\n");
            _writer.WriteAsync(messageBytes);
            _writer.FlushAsync();
        }

        public void RegisterNewPlayer(String name)
        {
            SendMessage("50" + name);
        }
        public void CreateNewTable(String tableName)
        {
            SendMessage("51" + tableName);
        }

        public void PlayerJoiningTable(Table table)
        {
            var liveTable = game.Tables.FirstOrDefault(t => t.Name == table.Name);

            if (liveTable != null)
            {
                int tableId = game.Tables.IndexOf(liveTable);

                PlayerStore.CurrentTable = liveTable;
                SendMessage("52" + tableId + PlayerStore.CurrentPlayer.Name);
            }
            else
            {
                OnLocalError?.Invoke("Error: Attempted to join a table that no longer exists in the game list.");
            }
        }

        public void PlayerLeavingTable(Table table)
        {
            SendMessage("53" + game.Tables.IndexOf(PlayerStore.CurrentTable) + PlayerStore.CurrentPlayer.Name);
            PlayerStore.CurrentTable = null;
        }

        /* --------------------------------------------------------
         * 
         * Recieiving network data 
         *
         -------------------------------------------------------*/
        private void InterpretMessage(String message)
        {
            string command = message.Substring(0, 2);
            string payload = message.Substring(2);

            switch (command)
            {
                case "00": UpdateGameState(message); break;
                case "01": game.AddPlayer(new Player(payload), false); break;
                case "02": game.RemovePlayer(game.GetPlayerFromName(payload)); break;
                case "03": game.AddTable(payload, false); break;
                case "04": throw new NotImplementedException(); break;
                case "05": UpdateTableState(payload); break;
                case "06": OnTableJoined?.Invoke(); break;
                case "07": OnTableLeft?.Invoke(); break;

                case "99": OnErrorReceived?.Invoke(payload); break;
            }
        }

        private void UpdateGameState(String message)
        {
            message = message.Remove(0, 2);
            XmlSerializer serializer = new XmlSerializer(typeof(Game));
            {
                if (serializer.Deserialize(new StringReader(message)) is Game deserializedGame)
                {
                    game.GameStateUpdate(deserializedGame);
                }
            }
        }

        private void UpdateTableState(String message)
        {
            int firstOpeningChar = message.IndexOf('<');
            int tableIndex = Int32.Parse(message.Substring(0, firstOpeningChar));
            String tableData = message.Substring(firstOpeningChar);

            XmlSerializer serializer = new XmlSerializer(typeof(Table));
            if (serializer.Deserialize(new StringReader(tableData)) is Table deserializedTable)
            {
                var dispatcher = App.MainDispatcher;
                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        game.Tables[tableIndex] = deserializedTable;
                        System.Diagnostics.Debug.WriteLine("Table state broadcast recieved succesfully.");
                        OnTableUpdated?.Invoke();
                    });
                }
                else
                {
                    game.Tables[tableIndex] = deserializedTable;
                    System.Diagnostics.Debug.WriteLine("Table state broadcast recieved succesfully.");
                    OnTableUpdated?.Invoke();
                }
            }
        }

        public void Disconnect()
        {
            _cts.Cancel();
            _writer?.Complete();
            _tcpClient?.Close();
            System.Diagnostics.Debug.WriteLine("Disconnected manually.");
        }
    }
}
