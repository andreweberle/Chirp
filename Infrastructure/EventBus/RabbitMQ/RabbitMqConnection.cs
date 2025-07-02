using System.Diagnostics;
using Chirp.Application.Interfaces;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

/// <summary>
/// Implementation of IRabbitMqConnection that manages connection to RabbitMQ
/// </summary>
internal class RabbitMqConnection(IConnectionFactory connectionFactory) : IRabbitMqConnection, IDisposable
{
    private static readonly Policy _policy = Policy
        .Handle<Exception>()
        .WaitAndRetry(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (ex, time) =>
            {
                Debug.WriteLine(
                    $"RabbitMQ Client could not connect after {time.TotalSeconds:n1}s ({ex.Message}). Retrying...");
            });

    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly Lock _lockSync = new();
    private IConnection? _connection;
    private bool _disposed;

    public void Dispose()
    {
        _connection?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Indicates if connection to RabbitMQ is established
    /// </summary>
    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    /// <summary>
    /// Creates a channel model for communication with RabbitMQ
    /// </summary>
    public IModel CreateModel()
    {
        if (!IsConnected) TryConnect();
        return _connection?.CreateModel()!;
    }

    /// <summary>
    /// Attempts to connect to RabbitMQ with retry policy
    /// </summary>
    public void TryConnect()
    {
        lock (_lockSync)
        {
            // Execute the policy.
            _policy.Execute(() =>
            {
                try
                {
                    _connection = _connectionFactory.CreateConnection();

                    if (IsConnected)
                    {
                        _connection.CallbackException += Connection_CallbackException;
                        _connection.ConnectionBlocked += Connection_ConnectionBlocked;
                        _connection.ConnectionShutdown += Connection_ConnectionShutdown;
                    }
                    else
                    {
                        // Retry connection establishment
                        throw new Exception("Connection not established.");
                    }
                }
                catch (Exception ex)
                {
                    // Handle any unanticipated exceptions here
                    Debug.WriteLine($"An error occurred during connection establishment: {ex.Message}");
                    throw; // Rethrow to let Polly handle retries
                }
            });
        }
    }

    private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        if (_disposed) return;
        TryConnect();
    }

    private void Connection_ConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        TryConnect();
    }

    private void Connection_CallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        TryConnect();
    }
}