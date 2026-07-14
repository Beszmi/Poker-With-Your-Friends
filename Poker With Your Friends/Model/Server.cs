using Poker_With_Your_Friends.ViewModel;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();

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
    }

    private readonly ConcurrentDictionary<string, PipeWriter> _connectedClients = new();
    private readonly ConcurrentDictionary<string, string> _clientToPlayerName = new();

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
                await SendGameStateAsync(clientId);
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
                    OnServerLoggedEvent?.Invoke($"Player {clientId} socket closed normally.");
                }
                _ = HandlePlayerDisconnectAsync(clientId);

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
        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent Table joined to [Broadcast]: {SerializedXML}");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(SerializedXML + "\n");

        List<string> deadClients = new List<string>();

        foreach (var kvp in _connectedClients)
        {
            string clientId = kvp.Key;
            PipeWriter writer = kvp.Value;

            try
            {
                await writer.WriteAsync(bytes);
            }
            catch (Exception ex)
            {
                OnServerLoggedEvent?.Invoke($"Broadcast failed for {clientId}. Error: {ex.Message}");
                deadClients.Add(clientId);
            }
        }

        foreach (string deadClient in deadClients)
        {
            if (_connectedClients.TryRemove(deadClient, out _))
            {
                OnServerLoggedEvent?.Invoke($"Player {deadClient} aggressively disconnected.");
                _ = HandlePlayerDisconnectAsync(deadClient);
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

        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"Sent message to <{clientId}>: {message}");
        }
    }

    private async Task HandlePlayerDisconnectAsync(string clientId)
    {
        if (_clientToPlayerName.TryRemove(clientId, out string playerName))
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

    public async void UpdateTable(Table t)
    {
        int TableIndex = game.Tables.IndexOf(t);

        await BroadcastTableUpdate(TableIndex, t);
    }

    public async void SendTimer(Table t, int s)
    {
        int TableIndex = game.Tables.IndexOf(t);

        await BroadcastTableTimer(TableIndex, s);
    }

    public async void SendTableText(Table t)
    {
        int TableIndex = game.Tables.IndexOf(t);

        await BroadcastTableText(TableIndex, t.TableText);
    }

    public void TableErrorHandler(String text)
    {
        OnServerLoggedEvent?.Invoke("🚨🚨🚨" + text + "🚨🚨🚨");
    }

    public async void PlayerEdit(String oldName, String? newName)
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
            await BroadcastPlayerNameEdit(oldName, newName!, player.Chips);
        }
        else
        {
            await BroadcastPlayerChipsEdit(oldName, player.Chips);
        }
    }

    /* --------------------------------------------------------
     * Recieiving network data 
     *
     -------------------------------------------------------*/

    private void InterpretMessage(string clientId, string message)
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
            case "50": RegisterNewPlayer(clientId, payload); break;
            case "51": CreateNewTable(payload); break;
            case "52": AddPlayerToTable(clientId, payload); break;
            case "53": RemovePlayerFromTable(clientId, payload); break;
            case "54": HandlePlayerAction(clientId, payload); break;
        }
    }

    private void RegisterNewPlayer(string clientId, string playerName)
    {
        if (game.DoesPlayerAlreadyExist(playerName) && IsPlayerLoggedIn(playerName))
        {
            BroadcastServerErrorClient(clientId, $"Someone already logged in as {playerName}");
            return;
        }
        _clientToPlayerName[clientId] = playerName;

        if (!game.DoesPlayerAlreadyExist(playerName))
        {
            game.AddPlayer(new Player(playerName), true);
            BroadcastNewPlayer(playerName);
        }
    }

    private void CreateNewTable(string message)
    {
        Table t = new Table(message);
        game.AddTable(t);
        BroadcastNewTable(t);
    }
    
    private async void AddPlayerToTable(string ClientId, string message)
    {
        int FirstOpeningChar = Utils.GetFirstNonNumberIndex(message);
        int TableIndex = Int32.Parse(message.Substring(0, FirstOpeningChar));
        if (game.GetPlayerFromName(message[FirstOpeningChar..]).Chips < game.Tables[TableIndex].SmallBlind+10)
        {
            BroadcastServerErrorClient(ClientId, $"You don't have enough chips to join this table's big blind ({game.Tables[TableIndex].SmallBlind+10}$)");
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
        SendJoinedTable(ClientId);
    }

    private async void RemovePlayerFromTable(string ClientId, String message)
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
        SendLeftTable(ClientId);
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
                OnServerLoggedEvent?.Invoke(
                    $"Client {clientId} sent action for '{playerName}' but is registered as '{registeredName}'.");
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
    /*public async Task BroadcastGameStateAsync() // Dont use unless necessary
    {
        string serializedGameState = GameStateSerializer();
        await BroadcastAsync(serializedGameState);
        OnServerLoggedEvent?.Invoke("Game state broadcast sent.");
        if (debugMessages)
        {
            OnServerLoggedEvent?.Invoke($"DEBUG: Sent to: [BROADCAST]:  {serializedGameState}");
        }
    }*/

    public async Task BroadcastNewPlayer(string playerName)
    {
        await BroadcastAsync("01" + playerName);
        OnServerLoggedEvent?.Invoke($"New player broadcast {playerName} (01)");
    }

    public async Task BroadcastDeletedPlayer(string playerName)
    {
        await BroadcastAsync("02" + playerName);
        OnServerLoggedEvent?.Invoke($"Player deleted broadcast {playerName} (02)");
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

    public async Task BroadcastTableTimer(int indexOfTable, int seconds)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("08,");
        sb.Append(indexOfTable);
        sb.Append(",");
        sb.Append(seconds);
        await BroadcastAsync(sb.ToString());
    }

    public async Task BroadcastTableText(int indexOfTable, String text)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("09,");
        sb.Append(indexOfTable);
        sb.Append(",");
        sb.Append(text);
        await BroadcastAsync(sb.ToString());
    }

    public async Task BroadcastPlayerChipsEdit(String name, int chips)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("10");
        sb.Append(name);
        sb.Append(",");
        sb.Append(chips);
        await BroadcastAsync(sb.ToString());
    }

    public async Task BroadcastPlayerNameEdit(String oldName, String newName, int chips)
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