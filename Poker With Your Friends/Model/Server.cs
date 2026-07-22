using Poker_With_Your_Friends.ViewModel;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Poker_With_Your_Friends.Model;

public class Server
{
    private const long MaxFrameBytes = 8 * 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private int _stopped;

    private Game game = Game.ServerInstance;

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
        Table.OnTimerStartRequest += SendTimer;
        Table.OnUpdateTextRequest += SendTableText;
        Table.OnTableLogicError += TableErrorHandler;
        ServerWindowViewModel.OnPlayerEdit += PlayerEdit;
        Table.OnCardRevealChanged += PlayerCardRevealedEdit;
    }

    private readonly ConcurrentDictionary<string, ClientConnection> _connectedClients = new();
    private readonly ConcurrentDictionary<string, string> _clientToPlayerName = new();

    private sealed class ClientConnection
    {
        private int _closed;

        public ClientConnection(TcpClient client, OutboundMessageQueue outbound)
        {
            Client = client;
            Outbound = outbound;
        }

        public TcpClient Client { get; }
        public OutboundMessageQueue Outbound { get; }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                Client.Close();
            }
        }
    }

    public bool debugMessages = false;

    private bool IsPlayerLoggedIn(String name)
    {
        return _clientToPlayerName.Values.Contains(name);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        OnServerLoggedEvent?.Invoke($"Server started on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                tcpClient.NoDelay = true;
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
            string clientId = Guid.NewGuid().ToString();
            var outbound = new OutboundMessageQueue(stream, _cts.Token);
            var connection = new ClientConnection(client, outbound);
            _connectedClients.TryAdd(clientId, connection);
            Task outboundMonitor = MonitorOutboundAsync(clientId, connection);

            try
            {
                await SendGameStateAsync(clientId);

                while (!_cts.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(_cts.Token);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryReadMessage(ref buffer, out ReadOnlySequence<byte> payload))
                    {
                        string opcode = Encoding.ASCII.GetString(payload.Slice(0, 2));
                        if (opcode == "57")
                        {
                            await HandlepfpRequest(clientId, payload);
                        }
                        else
                        {
                            await InterpretMessageAsync(clientId, Encoding.UTF8.GetString(payload));
                        }
                    }

                    if (buffer.Length > MaxFrameBytes)
                    {
                        throw new InvalidDataException(
                            $"Client {clientId} sent a frame larger than {MaxFrameBytes} bytes.");
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                OnServerLoggedEvent?.Invoke($"Client {clientId} disconnected with error: {ex.Message}");
            }
            finally
            {
                if (_connectedClients.TryRemove(clientId, out ClientConnection? removed))
                {
                    OnServerLoggedEvent?.Invoke($"Player {clientId} socket closed normally.");
                    removed.Close();
                }

                await reader.CompleteAsync();
                await outbound.DisposeAsync();
                await outboundMonitor;
                await HandlePlayerDisconnectAsync(clientId);
            }
        }
    }

    private async Task MonitorOutboundAsync(string clientId, ClientConnection connection)
    {
        try
        {
            await connection.Outbound.Completion;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            OnServerLoggedEvent?.Invoke(
                $"Outbound connection for {clientId} failed: {ex.Message}");
        }
        finally
        {
            connection.Close();
        }
    }

    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> payload)
    {
        const int headerSize = sizeof(int);

        if (buffer.Length < headerSize)
        {
            payload = ReadOnlySequence<byte>.Empty;
            return false;
        }

        int length = BitConverter.ToInt32(buffer.Slice(0, headerSize).ToArray());
        if (length <= 0 || length > MaxFrameBytes)
        {
            throw new InvalidDataException(
                $"Received a frame larger than {MaxFrameBytes} bytes.");
        }

        long frameSize = headerSize + (long)length;
        if (buffer.Length < frameSize)
        {
            payload = ReadOnlySequence<byte>.Empty;
            return false;
        }

        ReadOnlySequence<byte> output = buffer.Slice(headerSize, length);
        payload = output;
        buffer = buffer.Slice(frameSize);
        return true;
    }

    public Task BroadcastAsync(string SerializedXML)
    {
        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent XML to [Broadcast]: {SerializedXML}");
        }

        byte[] bytes = OutboundMessageQueue.EncodeFrame(SerializedXML);

        foreach (var kvp in _connectedClients)
        {
            string clientId = kvp.Key;
            ClientConnection connection = kvp.Value;

            if (!connection.Outbound.TryEnqueue(bytes))
            {
                OnServerLoggedEvent?.Invoke(
                    $"Disconnecting slow client {clientId}: outbound queue is full.");
                connection.Close();
            }
        }

        return Task.CompletedTask;
    }

    public Task BroadcastBytesAsync(ReadOnlySpan<byte> payload)
    {
        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent binary payload to [Broadcast] (Bytes): {payload.ToString()}");
        }

        byte[] bytes = OutboundMessageQueue.EncodeFrame(payload);

        foreach (var kvp in _connectedClients)
        {
            string clientId = kvp.Key;
            ClientConnection connection = kvp.Value;

            if (!connection.Outbound.TryEnqueue(bytes))
            {
                OnServerLoggedEvent?.Invoke(
                    $"Disconnecting slow client {clientId}: outbound queue is full.");
                connection.Close();
            }
        }

        return Task.CompletedTask;
    }

    private Task SendMessageAsync(string clientId, string message)
    {
        if (_connectedClients.TryGetValue(clientId, out ClientConnection? connection))
        {
            byte[] bytes = OutboundMessageQueue.EncodeFrame(message);
            if (!connection.Outbound.TryEnqueue(bytes))
            {
                OnServerLoggedEvent?.Invoke(
                    $"Failed to queue direct message for {clientId}; closing the connection.");
                connection.Close();
            }
        }
        else
        {
            OnServerLoggedEvent?.Invoke($"Attempted to send message to {clientId}, but they are not connected.");
        }

        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent message to <{clientId}>: {message}");
        }

        return Task.CompletedTask;
    }

    private Task SendBytesAsync(string clientId, ReadOnlySpan<byte> payload)
    {
        if (_connectedClients.TryGetValue(clientId, out ClientConnection? connection))
        {
            byte[] bytes = OutboundMessageQueue.EncodeFrame(payload);
            if (!connection.Outbound.TryEnqueue(bytes))
            {
                OnServerLoggedEvent?.Invoke(
                    $"Failed to queue direct message for {clientId}; closing the connection.");
                connection.Close();
            }
        }
        else
        {
            OnServerLoggedEvent?.Invoke($"Attempted to send message to {clientId}, but they are not connected.");
        }

        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent message to <{clientId}> (Bytes): {payload.ToString()}");
        }

        return Task.CompletedTask;
    }

    private async Task HandlePlayerDisconnectAsync(string clientId)
    {
        if (_clientToPlayerName.TryRemove(clientId, out string? playerName) && !string.IsNullOrEmpty(playerName))
        {
            OnServerLoggedEvent?.Invoke($"Handling disconnect for: {playerName}");

            try
            {
                Player disconnectedPlayer = game.GetPlayerFromName(playerName);

                foreach (var table in game.Tables)
                {
                    if (table.Players.Contains(disconnectedPlayer))
                    {
                        table.HandlePlayerDisconnected(disconnectedPlayer);
                    }
                }

                await BroadcastDeletedPlayer(playerName);
            }
            catch (ArgumentException)
            {
                // Player already removed or not found
            }
        }
    }

    /* --------------------------------------------------------
     * Server Logic
     *
     -------------------------------------------------------*/

    public void UpdateTable(Table t)
    {
        int TableIndex = game.Tables.IndexOf(t);
        ObserveNetworkTask(BroadcastTableUpdate(TableIndex, t), "table update");
    }

    public void SendTimer(Table t, int s)
    {
        int TableIndex = game.Tables.IndexOf(t);
        ObserveNetworkTask(BroadcastTableTimer(TableIndex, s), "table timer");
    }

    public void SendTableText(Table t)
    {
        int TableIndex = game.Tables.IndexOf(t);
        ObserveNetworkTask(BroadcastTableText(TableIndex, t.TableText), "table text");
    }

    public void TableErrorHandler(String text)
    {
        OnServerLoggedEvent?.Invoke("🚨🚨🚨" + text + "🚨🚨🚨");
    }

    public void PlayerEdit(String oldName, String? newName)
    {
        if (string.IsNullOrEmpty(oldName))
        {
            return;
        }

        bool nameChanged = !string.IsNullOrEmpty(newName)
            && !string.Equals(newName, oldName, StringComparison.Ordinal);
        string lookupName = nameChanged ? newName! : oldName;

        Player player;
        try
        {
            player = game.GetPlayerFromName(lookupName);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (nameChanged)
        {
            ObserveNetworkTask(
                BroadcastPlayerNameEdit(oldName, newName!, player.Chips),
                "player name edit");
        }
        else
        {
            ObserveNetworkTask(
                BroadcastPlayerChipsEdit(oldName, player.Chips),
                "player chip edit");
        }
    }

    private async void ObserveNetworkTask(Task operation, string operationName)
    {
        try
        {
            await operation;
        }
        catch (Exception ex)
        {
            OnServerLoggedEvent?.Invoke(
                $"Failed to send {operationName}: {ex.Message}");
        }
    }

    private async void PlayerCardRevealedEdit(String Name, bool NewValue)
    {
        await BroadcastPlayerCardRevealedEdit(Name, NewValue);
    }

    private async Task HandlepfpRequest(string? clientId, ReadOnlySequence<byte> transmission) //receives payload with opcode still in payload
    {
        if (string.IsNullOrEmpty(clientId)) return;

        // Optional filter: "57" = all, "57Alice" = one player
        string requestedName = Encoding.UTF8.GetString(transmission.Slice(2));

        foreach (Player player in game.Players)
        {
            if (!string.IsNullOrEmpty(requestedName) &&
                !string.Equals(player.Name, requestedName, StringComparison.Ordinal))
            {
                continue;
            }

            string customPath = Path.Combine(Game.PFPfilePath, $"{player.Name}pfp.jpg");
            if (!File.Exists(customPath)) continue;

            byte[] pfpFileData = await File.ReadAllBytesAsync(customPath);
            await SendPFPAsync(clientId, player.Name, pfpFileData);
        }

        //Default (Empty) profile picture sending
        String EmptyPFPDir = Path.Combine(Game.PFPfilePath, "Emptypfp.jpg");
        if (File.Exists(EmptyPFPDir))
        {
            byte[] pfpFileData = await File.ReadAllBytesAsync(EmptyPFPDir);
            await SendPFPAsync(clientId, "Empty", pfpFileData);
        }

    }

    /* --------------------------------------------------------
     * Recieiving network data 
     *
     -------------------------------------------------------*/

    private async Task InterpretMessageAsync(string clientId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length < 2)
        {
            System.Diagnostics.Debug.WriteLine($"Malformed message received from {clientId}. Length: {message?.Length}");
            return;
        }

        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"DEBUG: Recieved from [{clientId}]:  {message}");
        }

        string command = message.Substring(0, 2);
        string payload = message.Substring(2);

        switch (command)
        {
            case "50": await RegisterNewPlayerAsync(clientId, payload); break;
            case "51": await CreateNewTableAsync(payload); break;
            case "52": await AddPlayerToTableAsync(clientId, payload); break;
            case "53": await RemovePlayerFromTableAsync(clientId, payload); break;
            case "54": HandlePlayerAction(clientId, payload); break;
            case "55": await LoginPlayerAsync(clientId, payload); break;
            case "56": await HandlePlayerCardsRevealedChanged(payload); break;
        }
    }

    private async Task RegisterNewPlayerAsync(string clientId, string playerName)
    {
        if (game.DoesPlayerAlreadyExist(playerName))
        {
            await BroadcastServerErrorClient(clientId, $"Someone already Registered as {playerName}");
            return;
        }

        if (IsPlayerLoggedIn(playerName))
        {
            await BroadcastServerErrorClient(clientId, $"Someone already logged in as {playerName}");
            return;
        }

        _clientToPlayerName[clientId] = playerName;
        game.AddPlayer(new Player(playerName), true);
        await BroadcastNewPlayer(playerName);
    }

    private async Task LoginPlayerAsync(string clientId, string playerName)
    {
        if (!game.DoesPlayerAlreadyExist(playerName))
        {
            await BroadcastServerErrorClient(clientId, $"Player {playerName} is not registered");
            return;
        }

        if (IsPlayerLoggedIn(playerName))
        {
            await BroadcastServerErrorClient(clientId, $"Someone already logged in as {playerName}");
            return;
        }

        _clientToPlayerName[clientId] = playerName;
        // Other clients may have removed this player on disconnect; announce them again.
        await BroadcastNewPlayer(playerName);
    }

    private async Task CreateNewTableAsync(string message)
    {
        Table t = new Table(message);
        game.AddTable(t);
        await BroadcastNewTable(t);
    }
    
    private async Task AddPlayerToTableAsync(string ClientId, string message)
    {
        int FirstOpeningChar = Utils.GetFirstNonNumberIndex(message);
        int TableIndex = Int32.Parse(message.Substring(0, FirstOpeningChar));
        if (game.GetPlayerFromName(message[FirstOpeningChar..]).Chips < game.Tables[TableIndex].SmallBlind+10)
        {
            await BroadcastServerErrorClient(ClientId, $"You don't have enough chips to join this table's big blind ({game.Tables[TableIndex].SmallBlind+10}$)");
            return;
        }
        try
        {
            game.Tables[TableIndex].AddPlayer(game.GetPlayerFromName(message[FirstOpeningChar..]));
        }
        catch (InvalidOperationException ex)
        {
            await BroadcastServerErrorClient(ClientId, ex.Message);
            return;
        }

        await BroadcastTableUpdate(TableIndex, game.Tables[TableIndex]);
        await SendJoinedTable(ClientId);
    }

    private async Task RemovePlayerFromTableAsync(string ClientId, String message)
    {
        int FirstOpeningChar = Utils.GetFirstNonNumberIndex(message);
        int TableIndex = Int32.Parse(message.Substring(0, FirstOpeningChar));
        try
        {
            game.Tables[TableIndex].RemovePlayer(game.GetPlayerFromName(message[FirstOpeningChar..]));
        }
        catch (InvalidOperationException ex)
        {
            await BroadcastServerErrorClient(ClientId, ex.Message);
            return;
        }

        await BroadcastTableUpdate(TableIndex, game.Tables[TableIndex]);
        await SendLeftTable(ClientId);
    }

    private void HandlePlayerAction(string clientId, string actionData)
    {
        // format: "TableIndexPlayerName,ActionType,Amount" (e.g. "0Alice,Call,0" or "1Bob,Raise,20")
        int firstOpeningChar = Utils.GetFirstNonNumberIndex(actionData);
        if (firstOpeningChar <= 0)
        {
            OnServerLoggedEvent?.Invoke($"Malformed player action from {clientId}: '{actionData}'");
            return;
        }

        if (!int.TryParse(actionData.Substring(0, firstOpeningChar), out int tableIndex))
        {
            OnServerLoggedEvent?.Invoke($"Malformed player action from {clientId}: '{actionData}'");
            return;
        }

        string[] parts = actionData.Substring(firstOpeningChar).Split(',');
        if (parts.Length < 2)
        {
            OnServerLoggedEvent?.Invoke($"Malformed player action from {clientId}: '{actionData}'");
            return;
        }

        string playerName = parts[0];

        if (tableIndex < 0 || tableIndex >= game.Tables.Count)
        {
            OnServerLoggedEvent?.Invoke($"Player action referenced unknown table {tableIndex} from {clientId}");
            return;
        }

        if (!Enum.TryParse(parts[1], out Table.PlayerAction action))
        {
            OnServerLoggedEvent?.Invoke($"Unrecognized action '{parts[1]}' from {clientId}");
            return;
        }

        int amount = 0;
        if (parts.Length >= 3) int.TryParse(parts[2], out amount);

        if (_clientToPlayerName.TryGetValue(clientId, out string? registeredName))
        {
            if (!string.Equals(registeredName, playerName, StringComparison.Ordinal))
            {
                OnServerLoggedEvent?.Invoke($"Client {clientId} sent action for '{playerName}' but is registered as '{registeredName}'.");
                return;
            }
        }
        else
        {
            _clientToPlayerName[clientId] = playerName;
        }

        Player player;
        try
        {
            player = game.GetPlayerFromName(playerName);
        }
        catch (ArgumentException)
        {
            OnServerLoggedEvent?.Invoke($"Player '{playerName}' not found while submitting action.");
            return;
        }

        Table t = game.Tables[tableIndex];
        if (!t.SubmitPlayerAction(player, action, amount))
        {
            OnServerLoggedEvent?.Invoke($"Rejected out-of-turn/stale action '{action}' from {playerName}.");
        }
    }

    private async Task HandlePlayerCardsRevealedChanged(String Message)
    {
        string[] parts = Message.Split(',');
        if (parts.Length < 2 || !Boolean.TryParse(parts[0], out bool Revealed)) return;

        string playerName = parts[1];

        game.GetPlayerFromName(playerName).CardsRevealed = Revealed;

        await BroadcastPlayerCardRevealedEdit(playerName, Revealed);
    }

    /* --------------------------------------------------------
     * Sending network data
     *
     -------------------------------------------------------*/

    // Send message to 1 client

    private async Task SendJoinedTable(String ClientId)
    {
        await SendMessageAsync(ClientId, "06");
        OnServerLoggedEvent?.Invoke($"Sent Table joined to {ClientId} (06)");
    }

    private async Task SendLeftTable(String ClientId)
    {
        await SendMessageAsync(ClientId, "07");
        OnServerLoggedEvent?.Invoke($"Sent Table Left to {ClientId} (07)");
    }

    public async Task SendGameStateAsync(String ClientId) // Use to send whole game state to player on joining
    {
        string serializedGameState = GameStateSerializer();
        await SendMessageAsync(ClientId, serializedGameState);
        OnServerLoggedEvent?.Invoke("Game state broadcast sent.");
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

            serializer.Serialize(writer, Game.ServerInstance, namespaces);
        }
        
        return sb.ToString();
    }

    private async Task BroadcastNewPlayer(string playerName)
    {
        await BroadcastAsync("01" + playerName);
        OnServerLoggedEvent?.Invoke($"New player broadcast {playerName} (01)");
    }

    private async Task BroadcastDeletedPlayer(string playerName)
    {
        await BroadcastAsync("02" + playerName);
        OnServerLoggedEvent?.Invoke($"Player deleted broadcast {playerName} (02)");
    }

    private async Task BroadcastNewTable(Table table)
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
        OnServerLoggedEvent?.Invoke($"New table broadcast {table.Name}");
    }

    public async Task BroadcastServerErrorBroadcast(string ErrorMessage)
    {
        await BroadcastAsync("99" + ErrorMessage);
    }

    public async Task BroadcastServerErrorClient(String ClientId, string ErrorMessage)
    {
        await SendMessageAsync(ClientId, "99" + ErrorMessage);
    }

    private async Task BroadcastTableUpdate(int indexOfTable, Table t)
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

    private async Task BroadcastTableTimer(int indexOfTable, int seconds)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("08,");
        sb.Append(indexOfTable);
        sb.Append(",");
        sb.Append(seconds);
        await BroadcastAsync(sb.ToString());
    }

    private async Task BroadcastTableText(int indexOfTable, String text)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("09,");
        sb.Append(indexOfTable);
        sb.Append(",");
        sb.Append(text);
        await BroadcastAsync(sb.ToString());
    }

    private async Task BroadcastPlayerChipsEdit(String name, int chips)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("10");
        sb.Append(name);
        sb.Append(",");
        sb.Append(chips);
        await BroadcastAsync(sb.ToString());
    }

    private async Task BroadcastPlayerNameEdit(String oldName, String newName, int chips)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("11");
        sb.Append(oldName);
        sb.Append(",");
        sb.Append(newName);
        sb.Append(",");
        sb.Append(chips);
        await BroadcastAsync(sb.ToString());
    }

    private async Task BroadcastPlayerCardRevealedEdit(String name, bool Revealed)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("12");
        sb.Append(Revealed);
        sb.Append(",");
        sb.Append(name);
        await BroadcastAsync(sb.ToString());
    }

    private async Task SendPFPAsync(string clientId, string name, ReadOnlyMemory<byte> imageBytes)
    {
        // Payload: "13" + UInt16 LE nameLen + UTF-8 name + jpeg bytes
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length > ushort.MaxValue)
        {
            throw new InvalidDataException($"Player name is too long to send as a pfp frame: {name}");
        }

        byte[] preparedBytes = new byte[2 + sizeof(ushort) + nameBytes.Length + imageBytes.Length];
        preparedBytes[0] = (byte)'1';
        preparedBytes[1] = (byte)'3';
        BitConverter.TryWriteBytes(preparedBytes.AsSpan(2, sizeof(ushort)), (ushort)nameBytes.Length);
        nameBytes.CopyTo(preparedBytes.AsSpan(4));
        imageBytes.Span.CopyTo(preparedBytes.AsSpan(4 + nameBytes.Length));

        await SendBytesAsync(clientId, preparedBytes);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        _listener.Stop();

        foreach (ClientConnection connection in _connectedClients.Values)
        {
            connection.Close();
        }

        Table.OnUpdateTableRequest -= UpdateTable;
        Table.OnTimerStartRequest -= SendTimer;
        Table.OnUpdateTextRequest -= SendTableText;
        Table.OnTableLogicError -= TableErrorHandler;
        ServerWindowViewModel.OnPlayerEdit -= PlayerEdit;

        OnServerLoggedEvent?.Invoke("Server stopped.");
    }

    public static void InitializeServerTable(Table table)
    {
        table.InitializeServerTable();
    }
}