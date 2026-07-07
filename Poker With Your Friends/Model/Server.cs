using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model
{
    public class Server
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();

        private Game game = Game.Instance;

        public Action<String>? OnServerLoggedEvent;

        private XmlWriterSettings XMLsettings = new XmlWriterSettings
        {
            Indent = false,
            NewLineChars = "",
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = true
        };

        public Server(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            game.ServerMode = true;
            Table.OnUpdateTableRequest += UpdateTable;
        }

        private readonly ConcurrentDictionary<string, PipeWriter> _connectedClients = new();

        public async Task StartAsync()
        {
            _listener.Start();
            OnServerLoggedEvent?.Invoke($"Server started on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(tcpClient);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    OnServerLoggedEvent?.Invoke($"Accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            await using (NetworkStream stream = client.GetStream())
            {
                OnServerLoggedEvent?.Invoke("Client connected!");
                var reader = PipeReader.Create(stream);
                var writer = PipeWriter.Create(stream);

                string clientId = Guid.NewGuid().ToString();

                _connectedClients.TryAdd(clientId, writer);

                try
                {
                    await BroadcastGameStateAsync();
                }
                catch (Exception e)
                {
                    OnServerLoggedEvent?.Invoke($"Error: {e.Message}");
                    OnServerLoggedEvent?.Invoke($"Inner: {e.InnerException?.Message}");
                    OnServerLoggedEvent?.Invoke($"Inner-Inner: {e.InnerException?.InnerException?.Message}");
                }

                try
                {
                    while (true)
                    {
                        ReadResult result = await reader.ReadAsync();
                        ReadOnlySequence<byte> buffer = result.Buffer;

                        while (TryReadMessage(ref buffer, out string? message))
                        {
                            OnServerLoggedEvent?.Invoke($"Received Action from {clientId}: {message}");

                            InterpretMessage(clientId, message);
                        }

                        reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted) break;
                    }
                }
                catch (Exception ex)
                {
                    OnServerLoggedEvent?.Invoke($"Client {clientId} disconnected with error: {ex.Message}");
                }
                finally
                {
                    if (_connectedClients.TryRemove(clientId, out _))
                    {
                        OnServerLoggedEvent?.Invoke($"Player {clientId} successfully unregistered from broadcasts.");
                    }

                    await reader.CompleteAsync();
                    await writer.CompleteAsync();
                }
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

            ReadOnlySequence<byte> lineSlice = buffer.Slice(0, position.Value);
            message = Encoding.UTF8.GetString(lineSlice);

            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        public async Task BroadcastAsync(string SerializedXML)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(SerializedXML + "\n");

            foreach (var kvp in _connectedClients)
            {
                string playerId = kvp.Key;
                PipeWriter writer = kvp.Value;

                try
                {
                    await writer.WriteAsync(bytes);
                }
                catch (Exception ex)
                {
                    OnServerLoggedEvent?.Invoke($"Broadcast failed for {playerId}. Error: {ex.Message}");

                    if (_connectedClients.TryRemove(playerId, out _))
                    {
                        OnServerLoggedEvent?.Invoke($"Player {playerId} has been aggressively disconnected due to dead socket.");
                        // TODO: game.HandlePlayerDisconnect(playerId);
                    }

                    try { await writer.CompleteAsync(); } catch { /* Ignore */ }
                }
            }
        }

        private async Task SendMessageAsync(string clientId, string message)
        {
            if (_connectedClients.TryGetValue(clientId, out PipeWriter? writer))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
                try
                {
                    await writer.WriteAsync(bytes);
                }
                catch (Exception ex)
                {
                    OnServerLoggedEvent?.Invoke($"Failed to send direct message to {clientId}: {ex.Message}");
                }
            }
            else
            {
                OnServerLoggedEvent?.Invoke($"Attempted to send message to {clientId}, but they are not connected.");
            }
        }

        /* --------------------------------------------------------
         * Server Logic
         *
         -------------------------------------------------------*/

        public async void UpdateTable(Table t)
        {
            int TableIndex = game.Tables.IndexOf(t);

            await BroadcastTableUpdate(TableIndex, t);
        }

        /* --------------------------------------------------------
         * Recieiving network data 
         *
         -------------------------------------------------------*/

        private async Task InterpretMessage(string clientId, string message)
        {
            switch (message.Substring(0, 2))
            {
                case "50": RegisterNewPlayer(message.Remove(0, 2)); break;
                case "51": CreateNewTable(message.Remove(0, 2)); break;
                case "52": AddPlayerToTable(clientId, message.Remove(0, 2)); break;
                case "53": RemovePlayerFromTable(clientId, message.Remove(0, 2)); break;
                case "54": HandlePlayerAction(clientId, message.Remove(0, 2)); break;
            }
        }

        private void RegisterNewPlayer(string message)
        {
            game.AddPlayer(new Player(message), true);
            BroadcastNewPlayer(message);
        }

        private void CreateNewTable(string message)
        {
            Table t = new Table(message);
            game.AddTable(t);
            BroadcastNewTable(t);
        }
        
        private async void AddPlayerToTable(string clientId, string message)
        {
            int FirstOpeningChar = Utils.GetFirstNonNumberIndex(message);
            int TableIndex = Int32.Parse(message.Substring(0, FirstOpeningChar));
            try
            {
                game.Tables[TableIndex].AddPlayer(game.GetPlayerFromName(message[FirstOpeningChar..]));
            }
            catch (InvalidOperationException ex)
            {
                await BroadcastServerError(ex.Message);
                return;
            }

            await BroadcastTableUpdate(TableIndex, game.Tables[TableIndex]);
            SendJoinedTable(clientId);
        }

        private async void RemovePlayerFromTable(string clientId, String message)
        {
            int FirstOpeningChar = Utils.GetFirstNonNumberIndex(message);
            int TableIndex = Int32.Parse(message.Substring(0, FirstOpeningChar));
            try
            {
                game.Tables[TableIndex].RemovePlayer(game.GetPlayerFromName(message[FirstOpeningChar..]));
            }
            catch (InvalidOperationException ex)
            {
                await BroadcastServerError(ex.Message);
                return;
            }

            await BroadcastTableUpdate(TableIndex, game.Tables[TableIndex]);
            SendLeftTable(clientId);
        }

        private void HandlePlayerAction(string clientId, string actionData)
        {
            // Assume actionData format: "TableIndex,ActionType" (e.g., "0,Call")
            string[] parts = actionData.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int tableIndex))
            {
                Table t = game.Tables[tableIndex];
                if (Enum.TryParse(parts[1], out Table.PlayerAction action))
                {
                    t.PlayerActionTcs?.TrySetResult(action);
                }
            }
        }

        /* --------------------------------------------------------
         * Sending network data
         *
         -------------------------------------------------------*/

        // Send message to 1 client

        private async Task SendJoinedTable(String ClientId)
        {
            SendMessageAsync(ClientId, "06");
        }

        private async Task SendLeftTable(String ClientId)
        {
            SendMessageAsync(ClientId, "07");
        }

        //Broadcasts
        private string GameStateSerializer()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Game));
            StringBuilder sb = new StringBuilder();
            sb.Append("00");

            XmlWriterSettings settings = XMLsettings;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add("", "");

                serializer.Serialize(writer, Game.Instance, namespaces);
            }

            return sb.ToString();
        }
        public async Task BroadcastGameStateAsync()
        {
            string serializedGameState = GameStateSerializer();
            await BroadcastAsync(serializedGameState);
            OnServerLoggedEvent?.Invoke("Game state broadcast sent.");
        }

        public async Task BroadcastNewPlayer(string playerName)
        {
            await BroadcastAsync("01" + playerName);
        }

        public async Task BroadcastDeletedPlayer(string playerName)
        {
            await BroadcastAsync("02" + playerName);
        }

        public async Task BroadcastNewTable(Table table)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Table));
            StringBuilder sb = new StringBuilder();
            sb.Append("03");

            XmlWriterSettings settings = XMLsettings;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add("", "");

                serializer.Serialize(writer, table, namespaces);
            }

            await BroadcastAsync(sb.ToString());
        }

        public async Task BroadcastServerError(string ErrorMessage)
        {
            await BroadcastAsync("99" + ErrorMessage);
        }

        public async Task BroadcastTableUpdate(int indexOfTable, Table t)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Table));
            StringBuilder sb = new StringBuilder();
            sb.Append("05");
            sb.Append(indexOfTable);

            XmlWriterSettings settings = XMLsettings;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add("", "");

                serializer.Serialize(writer, t, namespaces);
            }

            await BroadcastAsync(sb.ToString());
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            OnServerLoggedEvent?.Invoke("Server stopped.");
        }

        public static void InitializeServerTable(Table table)
        {
            table.InitializeServerTable();
        }
    }
}