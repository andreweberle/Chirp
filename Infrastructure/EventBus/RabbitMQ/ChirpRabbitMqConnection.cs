using Chirp.Application.Interfaces;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Net.Sockets;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

internal sealed class ChirpRabbitMqConnection : IChirpRabbitMqConnection, IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly Lock _sync = new();

    private IConnection? _connection;
    private bool _disposed;
    private int _isReconnecting; // 0 = no, 1 = yes

    // Short, bounded retry with jitter
    private static readonly Random _rng = new();

    private static readonly AsyncRetryPolicy _retry =
        Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                5,
                attempt =>
                {
                    TimeSpan baseDelay =
                        TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1)); // 250ms, 500ms, 1s, 2s, 4s
                    TimeSpan jitter = TimeSpan.FromMilliseconds(_rng.Next(0, 250));
                    return baseDelay + jitter;
                },
                (ex, delay, attempt, ctx) =>
                {
                    Debug.WriteLine(
                        $"RabbitMQ connect retry {attempt} in {delay.TotalMilliseconds:n0} ms: {ex.Message}");
                    return Task.CompletedTask;
                });

    // Guard each connect attempt with a timeout so the app never sits forever
    private static readonly AsyncTimeoutPolicy _timeout = Policy.TimeoutAsync(TimeSpan.FromSeconds(5)); // per attempt

    private static readonly IAsyncPolicy _policy = Policy.WrapAsync(_retry, _timeout);

    public ChirpRabbitMqConnection(IConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public IModel CreateModel()
    {
        if (!IsConnected && !TryConnect())
        {
            throw new InvalidOperationException("RabbitMQ connection is not available.");
        }

        return _connection!.CreateModel();
    }

    /// <summary>
    /// Try to connect, returning false quickly if not possible.
    /// Never blocks the calling thread for long.
    /// </summary>
    public bool TryConnect()
    {
        if (_disposed) return false;

        using (_sync.EnterScope())
        {
            // Prevent concurrent reconnect loops
            if (_isReconnecting != 0) return IsConnected;

            _isReconnecting = 1;
        }

        try
        {
            // Run the connect attempt with timeout + retries OFF the lock
            IConnection connection = _policy.ExecuteAsync(async () =>
            {
                // Offload sync CreateConnection to a worker so timeout can cancel it
                Task<IConnection> task = Task.Run(() => _connectionFactory.CreateConnection());
                return await task; // timeout policy wraps this
            }).GetAwaiter().GetResult();

            // Only lock to swap the connection and attach handlers
            using (_sync.EnterScope())
            {
                _connection?.Dispose();
                _connection = connection;
                _connection.CallbackException += OnCallbackException;
                _connection.ConnectionBlocked += OnConnectionBlocked;
                _connection.ConnectionShutdown += OnConnectionShutdown;
            }

            Debug.WriteLine("RabbitMQ connected.");
            return true;
        }
        catch (TimeoutRejectedException)
        {
            Debug.WriteLine("RabbitMQ connect attempt timed out.");
            return false;
        }
        catch (BrokerUnreachableException ex)
        {
            Debug.WriteLine($"RabbitMQ unreachable: {ex.Message}");
            return false;
        }
        catch (SocketException ex)
        {
            Debug.WriteLine($"Socket error reaching RabbitMQ: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RabbitMQ connect failed: {ex.Message}");
            return false;
        }
        finally
        {
            using (_sync.EnterScope())
            {
                _isReconnecting = 0;
            }
        }
    }

    private void OnConnectionShutdown(object? s, ShutdownEventArgs e)
    {
        if (_disposed) return;
        Debug.WriteLine($"RabbitMQ shutdown: {e.ReplyText}");
        _ = TryConnect(); // fire-and-forget; guarded by _isReconnecting
    }

    private void OnConnectionBlocked(object? s, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        Debug.WriteLine($"RabbitMQ blocked: {e.Reason}");
        _ = TryConnect();
    }

    private void OnCallbackException(object? s, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        Debug.WriteLine($"RabbitMQ callback exception: {e.Exception.Message}");
        _ = TryConnect();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        using (_sync.EnterScope())
        {
            if (_connection != null)
            {
                _connection.CallbackException -= OnCallbackException;
                _connection.ConnectionBlocked -= OnConnectionBlocked;
                _connection.ConnectionShutdown -= OnConnectionShutdown;
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}