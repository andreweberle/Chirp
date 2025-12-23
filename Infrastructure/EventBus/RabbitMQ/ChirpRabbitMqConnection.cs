using Chirp.Application.Interfaces;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

internal sealed class ChirpRabbitMqConnection(IConnectionFactory connectionFactory) : IChirpRabbitMqConnection, IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;
    private int _isReconnecting; // 0 = no, 1 = yes

    private static readonly CreateChannelOptions _channelOptions = 
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

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
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        bool tryConnectResult = await TryConnectAsync(cancellationToken);
        
        if (!IsConnected && !tryConnectResult)
        {
            throw new InvalidOperationException("RabbitMQ connection is not available.");
        }


        return await _connection!.CreateChannelAsync(_channelOptions, cancellationToken);
    }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;

        // Gate concurrent reconnect loops
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            if (_isReconnecting != 0) return IsConnected;
            _isReconnecting = 1;
        }
        finally
        {
            _semaphoreSlim.Release();
        }

        try
        {
            // Run connect under Polly (timeout/retry/etc.)
            IConnection connection =
                await _policy.ExecuteAsync(
                        _ => _connectionFactory.CreateConnectionAsync(), // or (ct) => ... if supported
                        cancellationToken
                    )
                    .ConfigureAwait(false);

            // Swap connection under lock
            await _semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                _connection?.Dispose();
                _connection = connection;

                _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
                _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;

                Console.WriteLine("RabbitMQ connected.");
                return true;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
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
            // MUST reset even if caller cancels
            await _semaphoreSlim.WaitAsync(); // no token
            try
            {
                _isReconnecting = 0;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }

    private async Task OnConnectionShutdownAsync(object? s, ShutdownEventArgs e)
    {
        if (_disposed) return;
        
        // Don't reconnect on normal/initiated shutdowns
        if (e.Initiator == ShutdownInitiator.Application)
        {
            Console.WriteLine($"RabbitMQ shutdown (initiated by application): {e.ReplyText}");
            return;
        }
        
        Console.WriteLine($"RabbitMQ shutdown: {e.ReplyText} (Code: {e.ReplyCode}, Initiator: {e.Initiator})");
        
        // Only reconnect on unexpected shutdowns
        if (e.ReplyCode != Constants.ReplySuccess)
        {
            // Fire-and-forget reconnect to avoid blocking
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // Brief delay before reconnect
                await TryConnectAsync();
            });
        }
    }

    private async Task OnConnectionBlockedAsync(object? s, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ blocked: {e.Reason}");
        
        // Don't immediately try to reconnect when blocked
        // The connection will auto-recover when unblocked
        await Task.CompletedTask;
    }

    private async Task OnCallbackExceptionAsync(object? s, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ callback exception: {e.Exception.Message}");
        
        // Fire-and-forget reconnect
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Brief delay before reconnect
            await TryConnectAsync();
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _semaphoreSlim.WaitAsync();
        try
        {
            if (_connection != null)
            {
                _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                _connection.Dispose();
                _connection = null;
            }
        }
        finally
        {
            _semaphoreSlim.Release();
            _semaphoreSlim.Dispose();
        }
    }
}