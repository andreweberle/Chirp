using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of the event bus
/// </summary>
public class ChirpRabbitMqEventBus : EventBusBase, IAsyncDisposable
{
    private const string BrokerName = "chirp_event_bus";
    private readonly IChirpRabbitMqConnection _rabbitMQConnection;
    private readonly string _queueName;
    private readonly string _dlxQueueName;
    private readonly int _retryMax;
    
    private IChannel? _consumerChannel;
    
    // Single lock for all infrastructure changes (initialization and consumer starting)
    private readonly SemaphoreSlim _infrastructureLock = new(1, 1);
    
    private bool _isInfrastructureInitialized;
    private bool _isConsumerStarted;
    private string ExchangeName { get; }
    private string DlxExchangeName { get; }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of RabbitMQEventBus
    /// </summary>
    /// <param name="rabbitMQConnection">The RabbitMQ connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="queueName">The queue name</param>
    /// <param name="retryMax">Maximum number of retries</param>
    /// <param name="exchangeName">The exchange name</param>
    /// <param name="dlxExchangeName">Dead letter exchange name</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ChirpRabbitMqEventBus(
        IChirpRabbitMqConnection rabbitMQConnection,
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string queueName,
        int retryMax = 5,
        string exchangeName = BrokerName,
        string dlxExchangeName = "_dlxExchangeName")
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
        _rabbitMQConnection = rabbitMQConnection ?? throw new ArgumentNullException(nameof(rabbitMQConnection));
        _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
        _dlxQueueName = $"dlx.{_queueName}";
        _retryMax = retryMax;
        ExchangeName = exchangeName;
        DlxExchangeName = dlxExchangeName;
        Console.WriteLine("RabbitMQ event bus created. Infrastructure will be initialized on first use.");
    }

    /// <summary>
    /// Publishes an event to RabbitMQ
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override async Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure infrastructure is initialized
            await EnsureInfrastructureInitializedAsync(cancellationToken);

            // Ensure we are connected
            if (!_rabbitMQConnection.IsConnected) 
            {
                // Try to connect
                await _rabbitMQConnection.TryConnectAsync(cancellationToken);
            }

            // Create a channel
            await using IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);

            // Declare exchange (idempotent)
            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

            // Create message properties
            BasicProperties properties = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

            // Serialize event to JSON
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);

            // Publish the event
            await channel.BasicPublishAsync(
                exchange: ExchangeName, 
                routingKey: @event.GetType().Name, 
                mandatory: false, 
                basicProperties: properties, 
                body: payload, 
                cancellationToken: cancellationToken);
            
            // Log published event.
            return true;
        }
        catch (InvalidOperationException e)
        {
            // Log the error.
            Console.WriteLine(e);
            
            // Swallow the exception as the users application should not fail due to event bus issues.
            
            // TODO: Create a Global Error Event Handler So The User Can Handle Event Bus Errors.

            return false;
        }
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override async Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure infrastructure is initialized
            await EnsureInfrastructureInitializedAsync(cancellationToken);

            // Get the event name.
            string eventName = typeof(T).Name;

            // Log subscription
            Console.WriteLine($"--------------");
            Console.WriteLine($"Attempting to subscribe {typeof(TH).Name} to {eventName}");

            // Bind queue to exchange for this event type
            await using (IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken))
            {
                // Attempt to bind the queue.
                await channel.QueueBindAsync(_queueName, ExchangeName, eventName, cancellationToken: cancellationToken);
            }

            // Register subscription
            SubscriptionsManager.AddSubscription<T, TH>();

            // Start the consumer if not already started
            await StartConsumerAsync(cancellationToken);

            // Log subscription
            Console.WriteLine($"Subscribed {typeof(TH).Name} to {eventName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing {typeof(TH).Name}: {ex.Message}");
            throw;
        }
    }

    private async Task EnsureInfrastructureInitializedAsync(CancellationToken cancellationToken)
    {
        // Check if we are already initialized
        if (_isInfrastructureInitialized) return;

        // Acquire lock to initialize infrastructure
        await _infrastructureLock.WaitAsync(cancellationToken);
        
        try
        {
            // Double-check if initialized after acquiring lock
            if (_isInfrastructureInitialized) return;

            // Create the consumer channel (we keep this open)
            _consumerChannel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);
            _consumerChannel.CallbackExceptionAsync += OnConsumerChannelException;

            // Setup Dead Letter Exchange (DLX) and Queue
            await using (IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken))
            {
                // Declare a main exchange
                await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

                // Declare main queue with DLX settings
                await channel.QueueDeclareAsync(_queueName, true, false, false, null, cancellationToken: cancellationToken);

                // Setup DLX
                await channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

                // Setup DLX Queue
                await channel.QueueDeclareAsync(_dlxQueueName, true, false, false, null, cancellationToken: cancellationToken);

                // Bind DLX Queue to DLX Exchange
                await channel.QueueBindAsync(_dlxQueueName, DlxExchangeName, _dlxQueueName, cancellationToken: cancellationToken);
            }

            // Mark as initialized
            _isInfrastructureInitialized = true;
            Console.WriteLine("RabbitMQ infrastructure initialized.");
        }
        finally
        {
            _infrastructureLock.Release();
        }
    }

    private async Task StartConsumerAsync(CancellationToken cancellationToken)
    {
        // Check if consumer already started
        if (_isConsumerStarted) return;

        // Acquire lock to start a consumer
        await _infrastructureLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check if a consumer started after acquiring lock
            if (_isConsumerStarted) return;

            // Ensure consumer channel is available
            if (_consumerChannel == null || _consumerChannel.IsClosed)
            {
                // Create the consumer channel
                _consumerChannel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);

                // Register exception handler
                _consumerChannel.CallbackExceptionAsync += OnConsumerChannelException;
            }

            // Set prefetch count for fair dispatch,
            // This is to stop RMQ from sending multiple messages to a consumer.
            await _consumerChannel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: cancellationToken);

            // Create the async consumer
            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_consumerChannel);

            // Register the received event handler
            consumer.ReceivedAsync += OnMessageReceivedAsync;

            // Start consuming (this is the missing part)
            await _consumerChannel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            // Mark as started
            _isConsumerStarted = true;
        }
        finally
        {
            _infrastructureLock.Release();
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        string eventName = eventArgs.RoutingKey;
        string message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        int retryCount = 0;

        // Extract headers (retry count, original event name)
        if (eventArgs.BasicProperties.Headers != null)
        {
            // Get an original event name if present
            if (eventArgs.BasicProperties.Headers.TryGetValue("x-chirp-event", 
                out object? nameBytes) && nameBytes is byte[] bytes)
            {
                // Override event name from a header
                eventName = Encoding.UTF8.GetString(bytes);
            }

            // Get retry count if present
            if (eventArgs.BasicProperties.Headers.TryGetValue("x-chirp-retry", out object? countObj) 
                && int.TryParse(countObj?.ToString(), out int count))
            {
                retryCount = count;
            }
        }

        try
        {
            // Process the event
            await ProcessEvent(eventName, message);

            // Acknowledge the message
            await _consumerChannel!.BasicAckAsync(eventArgs.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event {eventName}: {ex.Message}");
            await HandleProcessingFailureAsync(eventArgs, eventName, retryCount, ex);
        }
    }

    private async Task HandleProcessingFailureAsync(BasicDeliverEventArgs eventArgs, string eventName, int retryCount, Exception ex)
    {
        if (retryCount < _retryMax)
        {
            // Retry: Publish back to the queue with incremented retry count
            BasicProperties properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    ["x-chirp-event"] = eventName,
                    ["x-chirp-retry"] = retryCount + 1,
                    ["x-chirp-exception"] = ex.Message
                }
            };

            // Simple backoff
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, retryCount + 1)));

            // Publish back to the queue
            await _consumerChannel!.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: eventName, 
                mandatory: false, 
                basicProperties: properties, 
                body: eventArgs.Body.ToArray());

            // Acknowledge the message
            await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, false);
        }
        else
        {
            // Dead Letter: Publish to DLX
            BasicProperties properties = new()
            {
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    ["x-chirp-event"] = eventName,
                    ["x-chirp-exception"] = ex.Message,
                    ["x-chirp-failed-at"] = DateTime.UtcNow.ToString("O")
                }
            };

            // We need a channel to publish to DLX (can use a consumer channel or new one)
            // Using a new one is safer to avoid blocking consumer
            await using IChannel channel = await _rabbitMQConnection.CreateChannelAsync();
            
            // Publish to DLX
            await channel.BasicPublishAsync(
                exchange: DlxExchangeName,
                routingKey: _dlxQueueName,
                mandatory: false,
                basicProperties: properties,
                body: eventArgs.Body.ToArray());

            // Acknowledge the message
            await _consumerChannel!.BasicAckAsync(eventArgs.DeliveryTag, false);
        }
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        // Process each handler
        await ProcessHandlers(eventName, message);
    }

    private async Task OnConsumerChannelException(object sender, CallbackExceptionEventArgs e)
    {
        // Log the exception
        Console.WriteLine($"Consumer channel error: {e.Exception.Message}");

        // Mark consumer as not started
        _isConsumerStarted = false;

        // Dispose the faulty channel
        _consumerChannel?.Dispose();
        _consumerChannel = null;
        
        // Try to recover
        await Task.Delay(1000);

        try 
        {
            // Restart the consumer
            await StartConsumerAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Swallow recovery errors to avoid crashing

            // Log the error
            Console.WriteLine($"Error restarting consumer: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes of resources used by the event bus
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Dispose infrastructure
        if (_consumerChannel != null)
        {
            if (_consumerChannel.IsOpen)
            {
                await _consumerChannel.CloseAsync();
            }
            await _consumerChannel.DisposeAsync();
        }
        _infrastructureLock.Dispose();
        GC.SuppressFinalize(this);
    }
}