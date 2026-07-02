using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace Poker_With_Your_Friends.Model
{
    public class Server
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentBag<PipeWriter> _connectedClients = new();

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
                var reader = PipeReader.Create(stream);
                var writer = PipeWriter.Create(stream);

                // Register this client for broadcasts
                _connectedClients.Add(writer);

                try
                {
                    while (true)
                    {
                        ReadResult result = await reader.ReadAsync();
                        ReadOnlySequence<byte> buffer = result.Buffer;

                        while (TryReadMessage(ref buffer, out string? message))
                        {
                            System.Diagnostics.Debug.WriteLine($"Received Action: {message}");

                            // 1. Process game logic here (e.g., Game.Instance.ProcessMove(message))
                            // 2. Broadcast updated state to EVERYONE
                            await BroadcastGameStateAsync($"New Game State after action: {message}");
                        }

                        reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted) break;
                    }
                }
                finally
                {
                    // Clean up on disconnect
                    await reader.CompleteAsync();
                    await writer.CompleteAsync();
                    // (Note: For production, remove writer from _connectedClients here)
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

        public async Task BroadcastGameStateAsync(string gameStateJson)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(gameStateJson + "\n");

            foreach (var writer in _connectedClients)
            {
                try
                {
                    await writer.WriteAsync(bytes);
                }
                catch
                {
                    // Handle dead connections gracefully
                }
            }
        }

        private async Task SendMessageAsync(PipeWriter writer, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await writer.WriteAsync(bytes);
        }
    }
}
