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

internal sealed class ChirpRabbitMqConnection(IConnectionFactory connectionFactory) : IChirpRabbitMqConnection, IDisposable
{
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly Lock _sync = new();

    private IConnection? _connection;
    private bool _disposed;
    private int _isReconnecting; // 0 = no, 1 = yes

    // Short, bounded retry with jitter
    private static readonly Random _rng = Random.Shared;

    private static readonly AsyncTimeoutPolicy<IConnection> _perAttemptTimeout = Policy.TimeoutAsync<IConnection>(TimeSpan.FromSeconds(5), TimeoutStrategy.Pessimistic);

    private static readonly AsyncRetryPolicy<IConnection> _retry =
        Policy<IConnection>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                5,
                attempt =>
                {
                    var baseDelay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1)); // 250, 500, 1000, 2000, 4000
                    var jitter = TimeSpan.FromMilliseconds(_rng.Next(0, 250));
                    return baseDelay + jitter;
                },
                (ex, delay, attempt, ctx) =>
                {
                    Console.WriteLine($"RabbitMQ connect retry {attempt} in {delay.TotalMilliseconds:n0} ms: {ex.Exception?.Message}");
                    return Task.CompletedTask;
                });

    // Guard each connect attempt with a timeout so the app never sits forever
    private static readonly AsyncTimeoutPolicy<IConnection> _overallTimeout = Policy.TimeoutAsync<IConnection>(TimeSpan.FromSeconds(15), TimeoutStrategy.Pessimistic);
    private static readonly Polly.Wrap.AsyncPolicyWrap<IConnection> _policy =Policy.WrapAsync(_overallTimeout, _retry, _perAttemptTimeout);

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
            IConnection connection = _policy.ExecuteAsync(async () =>
            {
                // Offload the blocking connect so pessimistic timeout can cut it off
                Task<IConnection> connectTask = Task.Run(() => _connectionFactory.CreateConnection());
                return await connectTask.ConfigureAwait(false);
            })
                .GetAwaiter().GetResult();

            // Only lock to swap the connection and attach handlers
            using (_sync.EnterScope())
            {
                _connection?.Dispose();
                _connection = connection;
                _connection.CallbackException += OnCallbackException;
                _connection.ConnectionBlocked += OnConnectionBlocked;
                _connection.ConnectionShutdown += OnConnectionShutdown;
            }

            Console.WriteLine("RabbitMQ connected.");
            return true;
        }
        catch (TimeoutRejectedException)
        {
            Console.WriteLine("RabbitMQ connect attempt timed out.");
            return false;
        }
        catch (BrokerUnreachableException ex)
        {
            Console.WriteLine($"RabbitMQ unreachable: {ex.Message}");
            return false;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Socket error reaching RabbitMQ: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RabbitMQ connect failed: {ex.Message}");
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
        Console.WriteLine($"RabbitMQ shutdown: {e.ReplyText}");
        _ = TryConnect(); // fire-and-forget; guarded by _isReconnecting
    }

    private void OnConnectionBlocked(object? s, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ blocked: {e.Reason}");
        _ = TryConnect();
    }

    private void OnCallbackException(object? s, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ callback exception: {e.Exception.Message}");
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