using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
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

        private readonly ConcurrentDictionary<PipeWriter, string> _connectedClients = new();

        private Game game = Game.Instance;

        public Server(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            System.Diagnostics.Debug.WriteLine($"Server started on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(tcpClient);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"Accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            await using (NetworkStream stream = client.GetStream())
            {
                System.Diagnostics.Debug.WriteLine("Client connected!");
                var reader = PipeReader.Create(stream);
                var writer = PipeWriter.Create(stream);

                string clientId = Guid.NewGuid().ToString();
                _connectedClients.TryAdd(writer, clientId);

                try
                {
                    await BroadcastGameStateAsync();
                } catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {e.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner: {e.InnerException?.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner-Inner: {e.InnerException?.InnerException?.Message}");
                }

                try
                {
                    while (true)
                    {
                        ReadResult result = await reader.ReadAsync();
                        ReadOnlySequence<byte> buffer = result.Buffer;

                        while (TryReadMessage(ref buffer, out string? message))
                        {
                            System.Diagnostics.Debug.WriteLine($"Received Action from {clientId}: {message}");

                            InterpretMessage(clientId, message);

                            await BroadcastAsync($"New Game State after action: {message}");
                        }

                        reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted) break; // Client gracefully disconnected
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Client {clientId} disconnected with error: {ex.Message}");
                }
                finally
                {
                    if (_connectedClients.TryRemove(writer, out _))
                    {
                        System.Diagnostics.Debug.WriteLine($"Player {clientId} successfully unregistered from broadcasts.");
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

            foreach (PipeWriter writer in _connectedClients.Keys)
            {
                try
                {
                    await writer.WriteAsync(bytes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Broadcast failed. Error: {ex.Message}");

                    if (_connectedClients.TryRemove(writer, out string? playerId))
                    {
                        System.Diagnostics.Debug.WriteLine($"Player {playerId} has been aggressively disconnected due to dead socket.");
                        // TODO: game.HandlePlayerDisconnect(playerId);
                    }

                    try { await writer.CompleteAsync(); } catch { /* Ignore */ }
                }
            }
        }

        // Send message to 1 client
        private async Task SendMessageAsync(PipeWriter writer, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await writer.WriteAsync(bytes);
        }

        private async Task InterpretMessage(string clientId, string message)
        {
            // TODO: Implement your game logic update here.
        }

        private string GameStateSerializer()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Game));
            StringBuilder sb = new StringBuilder();

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineChars = "",
                NewLineHandling = NewLineHandling.None,
                OmitXmlDeclaration = true
            };

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
            System.Diagnostics.Debug.WriteLine("Game state broadcast sent.");
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            System.Diagnostics.Debug.WriteLine("Server stopped.");
        }

        public static void InitializeServerTable(Table table)
        {
            table.InitializeServerTable();
        }
    }
}