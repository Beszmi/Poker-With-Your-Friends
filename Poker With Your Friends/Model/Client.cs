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

namespace Poker_With_Your_Friends.Model;

public class Client
{
    private const long MaxFrameBytes = 8 * 1024 * 1024;

    public IPlayerStore PlayerStore { get; }

    public TimerService TimerService { get; } = new TimerService();
    
    public event Action<String>? OnErrorReceived;
    public event Action<String>? OnLocalError;
    public event Action? OnTableUpdated;
    public event Action? OnTableJoined;
    public event Action? OnTableLeft;
    public String Host { get; set; }
    public int Port { get; set; }

    private OutboundMessageQueue? _outbound;
    private TcpClient? _tcpClient;
    private CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private Task? _outboundMonitorTask;
    private int _disconnected;

    private Game game = Game.ClientInstance;

    public Client(String host, int port, IPlayerStore playerStore)
    {
        Host = host;
        Port = port;
        this.PlayerStore = playerStore;

        InGamePage.OnJoinGameClick += PlayerJoiningTable;
        InGamePage.OnLeaveGameClick += PlayerLeavingTable;
    }

    public async Task ConnectAndRunAsync()
    {
        _tcpClient = new TcpClient();
        try
        {
            await _tcpClient.ConnectAsync(Host, Port, _cts.Token);
            _tcpClient.NoDelay = true;
            System.Diagnostics.Debug.WriteLine("Connected to server!");

            var stream = _tcpClient.GetStream();
            var reader = PipeReader.Create(stream);
            _outbound = new OutboundMessageQueue(stream, _cts.Token);

            _receiveTask = ReceiveLoopAsync(reader, _cts.Token);
            _outboundMonitorTask = MonitorOutboundAsync(_outbound);
        }
        catch
        {
            _tcpClient.Close();
            throw;
        }
    }

    private async Task ReceiveLoopAsync(PipeReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out string response))
                {
                    InterpretMessage(response);

                    System.Diagnostics.Debug.WriteLine($"[Game Update]: {response}");
                }

                if (buffer.Length > MaxFrameBytes)
                {
                    throw new InvalidDataException(
                        $"Received a frame larger than {MaxFrameBytes} bytes.");
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted)
                {
                    if (!token.IsCancellationRequested)
                    {
                        OnLocalError?.Invoke("The server closed the connection.");
                    }
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {}
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connection lost: {ex.Message}");
            OnLocalError?.Invoke($"Connection lost: {ex.Message}");
        }
        finally
        {
            _outbound?.Abort();
            _tcpClient?.Close();
            await reader.CompleteAsync();
        }
    }

    private async Task MonitorOutboundAsync(OutboundMessageQueue outbound)
    {
        try
        {
            await outbound.Completion;
        }
        catch (OperationCanceledException) {}
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Send failed: {ex.Message}");
            if (!_cts.IsCancellationRequested)
            {
                OnLocalError?.Invoke($"Failed to send to server: {ex.Message}");
            }
        }
        finally
        {
            _tcpClient?.Close();
        }
    }

    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out string message)
    {
        SequencePosition? position = buffer.PositionOf((byte)'\n');
        if (position == null)
        {
            message = string.Empty;
            return false;
        }
        ReadOnlySequence<byte> lineSlice = buffer.Slice(0, position.Value);
        if (lineSlice.Length > MaxFrameBytes)
        {
            throw new InvalidDataException(
                $"Received a frame larger than {MaxFrameBytes} bytes.");
        }
        message = Encoding.UTF8.GetString(lineSlice);
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
        OutboundMessageQueue? outbound = _outbound;
        if (outbound == null)
        {
            OnLocalError?.Invoke("Cannot send because the client is not connected.");
            return;
        }

        byte[] messageBytes = OutboundMessageQueue.EncodeFrame(message);
        if (!outbound.TryEnqueue(messageBytes))
        {
            OnLocalError?.Invoke(
                "The connection cannot keep up with outgoing messages and was closed.");
            _tcpClient?.Close();
        }
    }

    public void RegisterNewPlayer(String name)
    {
        SendMessage("50" + name);
    }

    public void LoginPlayer(String name)
    {
        SendMessage("55" + name);
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
            SendMessage("52" + tableId + PlayerStore.CurrentPlayer?.Name);
        }
        else
        {
            OnLocalError?.Invoke("Error: Attempted to join a table that no longer exists in the game list.");
        }
    }

    public void PlayerLeavingTable(Table table)
    {
        var liveTable = game.Tables.FirstOrDefault(t => t.Name == table.Name)
            ?? PlayerStore.CurrentTable;
        int tableId = liveTable != null ? game.Tables.IndexOf(liveTable) : -1;
        string? playerName = PlayerStore.CurrentPlayer?.Name;

        if (tableId < 0 || string.IsNullOrEmpty(playerName)) return;

        SendMessage("53" + tableId + playerName);
        PlayerStore.CurrentTable = null;
    }

    /* --------------------------------------------------------
     * 
     * Recieiving network data 
     *
     -------------------------------------------------------*/
    private void InterpretMessage(String message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length < 2)
        {
            System.Diagnostics.Debug.WriteLine("Ignored malformed server message.");
            return;
        }

        string command = message.Substring(0, 2);
        string payload = message.Substring(2);

        System.Diagnostics.Debug.WriteLine("Client got message" + message);

        switch (command)
        {
            case "00": UpdateGameState(message); break;
            case "01": PlayerLogin(payload); break;
            case "02": game.RemovePlayer(game.GetPlayerFromName(payload)); break;
            case "03": game.AddTable(payload, false); break;
            case "04": throw new NotImplementedException(); break;
            case "05": UpdateTableState(payload); break;
            case "06": OnTableJoined?.Invoke(); break;
            case "07": OnTableLeft?.Invoke(); break;
            case "08": StartTableTimer(message); break;
            case "09": SetTableText(message); break;
            case "10": UpdatePlayerChips(payload); break;
            case "11": UpdatePlayerNameAndChips(payload); break;

            case "99": OnErrorReceived?.Invoke(payload); break;
        }
    }

    private void RunOnUiThread(Action action)
    {
        var dispatcher = App.MainDispatcher;
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => action());
        }
        else
        {
            action();
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

    private void PlayerLogin(String message)
    {
        if (!game.DoesPlayerAlreadyExist(message))
            RunOnUiThread(() =>
            { game.AddPlayer(new Player(message), false); });
    }

    private void UpdateTableState(String message)
    {
        int firstOpeningChar = message.IndexOf('<');
        int tableIndex = Int32.Parse(message.Substring(0, firstOpeningChar));
        String tableData = message.Substring(firstOpeningChar);

        XmlSerializer serializer = new XmlSerializer(typeof(Table));
        if (serializer.Deserialize(new StringReader(tableData)) is Table deserializedTable)
        {
            RunOnUiThread(() =>
            {
                Table.HandleUpdateFromNetwork(tableIndex, deserializedTable);
                System.Diagnostics.Debug.WriteLine("Table state broadcast recieved succesfully.");
                OnTableUpdated?.Invoke();
            });
        }
    }

    private void StartTableTimer(String message)
    {
        // Message format: "08,{tableIndex},{seconds}"
        string[] parts = message.Split(',');
        if (parts.Length < 3) return;

        int tableIndex = Int32.Parse(parts[1]);
        int seconds = Int32.Parse(parts[2]);

        RunOnUiThread(() => StartTableTimerOnUiThread(tableIndex, seconds));
    }

    private void StartTableTimerOnUiThread(int tableIndex, int seconds)
    {
        if (tableIndex < 0 || tableIndex >= game.Tables.Count) return;

        TimerService.GetOrCreateTimer(game.Tables[tableIndex]).StartTimer(seconds);
    }

    public void SendPlayerAction(Table.PlayerAction action, int amount)
    {
        int tableIndex = Table.GetTableIdByName(PlayerStore.CurrentTable?.Name ?? "");
        string? playerName = PlayerStore.CurrentPlayer?.Name;
        if (tableIndex != -1 && !string.IsNullOrEmpty(playerName))
        {
            SendMessage($"54{tableIndex}{playerName},{action},{amount}");
        }
    }

    private void SetTableText(String message)
    {
        if (string.IsNullOrEmpty(message)) return;

        int firstComma = message.IndexOf(',');
        if (firstComma < 0) return;

        int secondComma = message.IndexOf(',', firstComma + 1);
        if (secondComma < 0) return;


        int tableIndex = Int32.Parse(message.Substring(firstComma + 1, secondComma - firstComma - 1));
        string text = message.Substring(secondComma + 1);

        if (tableIndex < 0 || tableIndex >= game.Tables.Count) return;

        RunOnUiThread(() => game.Tables[tableIndex].TableText = text);
    }

    private void UpdatePlayerChips(string payload)
    {
        string[] parts = payload.Split(',');
        if (parts.Length < 2 || !Int32.TryParse(parts[1], out int chips)) return;

        string playerName = parts[0];
        RunOnUiThread(() =>
        {
            try
            {
                game.GetPlayerFromName(playerName).Chips = chips;
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePlayerChips failed: {ex.Message}");
            }
        });
    }

    private void UpdatePlayerNameAndChips(string payload)
    {
        string[] parts = payload.Split(',');
        if (parts.Length < 3 || !Int32.TryParse(parts[2], out int chips))
        {
            return;
        }

        string oldName = parts[0];
        string newName = parts[1];

        RunOnUiThread(() =>
        {
            try
            {
                Player player = game.GetPlayerFromName(oldName);
                player.Chips = chips;

                if (!string.Equals(player.Name, newName, StringComparison.Ordinal))
                {
                    player.Name = newName;
                    game.RefreshPlayerNames();

                    if (string.Equals(PlayerStore.CurrentPlayer?.Name, oldName, StringComparison.Ordinal))
                    {
                        PlayerStore.CurrentPlayer = player;
                    }
                }
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePlayerNameAndChips failed: {ex.Message}");
            }
        });
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) != 0) return;

        InGamePage.OnJoinGameClick -= PlayerJoiningTable;
        InGamePage.OnLeaveGameClick -= PlayerLeavingTable;

        _cts.Cancel();
        _tcpClient?.Close();
        _outbound?.Abort();

        try
        {
            _outbound?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _receiveTask?.GetAwaiter().GetResult();
            _outboundMonitorTask?.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Disconnect cleanup failed: {ex.Message}");
        }

        _outbound = null;
        game.Clear();
        PlayerStore.CurrentPlayer = null;
        PlayerStore.CurrentTable = null;
        System.Diagnostics.Debug.WriteLine("Disconnected manually.");
    }
}
