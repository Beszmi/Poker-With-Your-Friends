using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Poker_With_Your_Friends.Model;

/// <summary>
/// Serializes all writes for one TCP connection. Producers may enqueue
/// concurrently, but only the write loop ever touches the network stream.
/// </summary>
internal sealed class OutboundMessageQueue : IAsyncDisposable
{
    private const int DefaultCapacity = 256;

    private readonly Channel<ReadOnlyMemory<byte>> _messages;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts;
    private readonly Task _writeLoop;
    private int _stopping;

    public OutboundMessageQueue(
        NetworkStream stream,
        CancellationToken shutdownToken = default,
        int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _stream = stream;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        _messages = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        _writeLoop = WriteLoopAsync();
    }

    public Task Completion => _writeLoop;

    // Text messages (XML / opcodes) — same length-prefixed framing as binary.
    public static byte[] EncodeFrame(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return EncodeFrame(Encoding.UTF8.GetBytes(message));
    }

    // Length-prefixed frame: [4-byte LE length][payload]
    public static byte[] EncodeFrame(ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[sizeof(int) + payload.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, sizeof(int)), payload.Length);
        payload.CopyTo(frame.AsSpan(sizeof(int)));
        return frame;
    }

    public bool TryEnqueue(ReadOnlyMemory<byte> frame)
    {
        if (frame.IsEmpty || Volatile.Read(ref _stopping) != 0) return false;

        return _messages.Writer.TryWrite(frame);
    }

    public ValueTask EnqueueAsync(
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken = default)
    {
        if (frame.IsEmpty)
        {
            throw new ArgumentException("A network frame cannot be empty.", nameof(frame));
        }

        if (Volatile.Read(ref _stopping) != 0)
        {
            return ValueTask.FromException(
                new IOException("The connection is no longer accepting messages."));
        }

        return _messages.Writer.WriteAsync(frame, cancellationToken);
    }

    public void Abort()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 0)
        {
            _messages.Writer.TryComplete();
            _cts.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Abort();

        try
        {
            await _writeLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) {}
        catch (IOException) {}
        catch (SocketException) {}
        finally
        {
            _cts.Dispose();
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (ReadOnlyMemory<byte> frame in
                _messages.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await _stream.WriteAsync(frame, _cts.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            _messages.Writer.TryComplete();
        }
    }
}
