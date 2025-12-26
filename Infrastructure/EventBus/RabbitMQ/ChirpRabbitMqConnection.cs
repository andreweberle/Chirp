using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

internal sealed class ChirpRabbitMqConnection(
    IConnectionFactory connectionFactory, 
    ChirpLogger logger) : IChirpRabbitMqConnection, IAsyncDisposable
{
    private readonly ChirpLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        // Check if already connected
        if (IsConnected) return await _connection!.CreateChannelAsync(_channelOptions, cancellationToken);
        
        // Attempt to connect.
        await TryConnectAsync(cancellationToken);

        // Check if still connected after connecting.
        if (IsConnected) return await _connection!.CreateChannelAsync(_channelOptions, cancellationToken);
        
        // Log error and throw exception if connection is not available.
        _logger.Log(LogLevel.Error, 0, "RabbitMQ connection is not available.", null, (s, e) => s);
        
        // Throw exception if connection is not available.
        throw new InvalidOperationException("RabbitMQ connection is not available.");
    }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        if (IsConnected) return true;

        // Acquire the connection lock.
        await _connectionLock.WaitAsync(cancellationToken);
        
        try
        {
            // Double-check if connected after acquiring lock
            if (IsConnected) return true;

            // Try to connect using the retry policy
            _connection = await _retryPolicy.ExecuteAsync(async () => 
                await _connectionFactory.CreateConnectionAsync(cancellationToken));

            if (!IsConnected) return false;
            
            _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
            _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
            _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

            _logger.LogInformation("RabbitMQ connecting...");
            return true;

        }
        catch (Exception ex)
        {
            if (!_logger.IsEnabled(LogLevel.Error)) return false;
            _logger.LogError(ex, "RabbitMQ connect failed: {Message}", ex.Message);
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
            if (!_logger.IsEnabled(LogLevel.Information)) return;
            _logger.LogInformation("RabbitMQ shutting down...");
            _logger.LogInformation("RabbitMQ shutdown (application): {ReplyText}", e.ReplyText);
            return;
        }
        
        // Attempt to reconnect.
        await ReconnectAsync();
    }

    private async Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(e.Exception, "RabbitMQ callback exception: {Message}", e.Exception.Message);   
        }
        
        // Attempt to reconnect.
        await ReconnectAsync();
    }

    private async Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        if (!_logger.IsEnabled(LogLevel.Warning)) return;
        _logger.LogWarning("RabbitMQ blocked: {Reason}", e.Reason);
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
            if (!_logger.IsEnabled(LogLevel.Error)) return;
            _logger.LogError(ex, "Error disposing RabbitMQ connection: {Message}", ex.Message);
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }
}