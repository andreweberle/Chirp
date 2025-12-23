using Chirp.Application.Interfaces;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

internal sealed class ChirpRabbitMqConnection(IConnectionFactory connectionFactory) : IChirpRabbitMqConnection, IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    
    // Lock to ensure only one connection attempt happens at a time
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;

    // Options for creating channels (enable publisher confirms)
    private static readonly CreateChannelOptions _channelOptions = 
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    // Simple retry policy: Retry 5 times with exponential backoff
    private static readonly AsyncRetryPolicy<IConnection> _retryPolicy = Policy<IConnection>
        .Handle<Exception>()
        .WaitAndRetryAsync(
            5,
            attempt => TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1)),
            (outcome, delay, attempt, ctx) =>
            {
                Console.WriteLine($"RabbitMQ connect retry {attempt} in {delay.TotalMilliseconds:n0} ms: {outcome.Exception?.Message}");
                return Task.CompletedTask;
            });

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken);
            
            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ connection is not available.");
            }
        }

        return await _connection!.CreateChannelAsync(_channelOptions, cancellationToken);
    }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        if (IsConnected) return true;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check if connected after acquiring lock
            if (IsConnected) return true;

            // Try to connect using the retry policy
            _connection = await _retryPolicy.ExecuteAsync(async () => 
                await _connectionFactory.CreateConnectionAsync(cancellationToken));

            if (IsConnected)
            {
                _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

                Console.WriteLine("RabbitMQ connected.");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RabbitMQ connect failed: {ex.Message}");
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        if (_disposed) return;

        if (e.Initiator == ShutdownInitiator.Application)
        {
            Console.WriteLine($"RabbitMQ shutdown (application): {e.ReplyText}");
            return;
        }

        Console.WriteLine($"RabbitMQ shutdown (unexpected): {e.ReplyText}");
        await ReconnectAsync();
    }

    private async Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ callback exception: {e.Exception.Message}");
        await ReconnectAsync();
    }

    private async Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        Console.WriteLine($"RabbitMQ blocked: {e.Reason}");
    }

    private async Task ReconnectAsync()
    {
        // Wait a bit before trying to reconnect
        await Task.Delay(1000);
        await TryConnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null)
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing RabbitMQ connection: {ex.Message}");
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }
}