using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chirp.Infrastructure.EventBus.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of the event bus
/// </summary>
public class ChirpRabbitMqEventBus : EventBusBase
{
    private const string BrokerName = "chirp_event_bus";

    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve
    };

    private static BasicProperties CreateBasicProperties() => new()
    {
        DeliveryMode = DeliveryModes.Persistent
    };

    private readonly string _dlxQueueName;
    private readonly string _queueName;
    private readonly IChirpRabbitMqConnection _rabbitMQConnection;
    private readonly int _retryMax;
    private IChannel? _consumerChannel;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private bool _initialized = false;

    private string ExchangeName { get; }
    private string DlxExchangeName { get; }

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
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentNullException(nameof(queueName), "Queue name must be provided.");

        _rabbitMQConnection = rabbitMQConnection ?? throw new ArgumentNullException(nameof(rabbitMQConnection));
        ExchangeName = exchangeName ?? throw new ArgumentNullException(nameof(exchangeName));
        DlxExchangeName = dlxExchangeName ?? throw new ArgumentNullException(nameof(dlxExchangeName));

        _queueName = queueName;
        _dlxQueueName = $"dlx.{_queueName}";
        _retryMax = retryMax;

        // Defer initialization to allow application to start even if RabbitMQ is unavailable
        // This prevents UseChirp() from blocking application startup
        try
        {
            // Fix: Remove 'await' and call synchronously, or move this logic to an async factory/init method.
            InitializeInfrastructureAsync().GetAwaiter().GetResult();
            _initialized = true;
        }
        catch (Exception ex)
        {
            // TODO: Create flag to control whether to throw or not
            Console.WriteLine($"Warning: Failed to initialize RabbitMQ infrastructure during startup: {ex.Message}");
            throw;
            //Console.WriteLine("The event bus will attempt to reconnect on first use.");
            // Don't throw - allow the application to start
            // Initialization will be retried on first Publish/Subscribe call
        }
    }

    /// <summary>
    /// Initializes the RabbitMQ infrastructure (exchanges, queues, channels)
    /// </summary>
    private async Task InitializeInfrastructureAsync(CancellationToken cancellationToken = default)
    {
        // Create consumer channel
        _consumerChannel = await CreateConsumerChannelAsync(cancellationToken);

        // Create DLX infrastructure
        using IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);

        // Create Exchange.
        await channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

        // Create the Queue
        await channel.QueueDeclareAsync(_dlxQueueName, true, false, false, null, cancellationToken: cancellationToken);

        // Bind DLXQ To DLX
        await channel.QueueBindAsync(_dlxQueueName, DlxExchangeName, _dlxQueueName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ensures infrastructure is initialized, attempting to initialize if not already done
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _semaphoreSlim.WaitAsync();

        try
        {
            if (_initialized) return;

            try
            {
                await InitializeInfrastructureAsync();
                _initialized = true;
                Console.WriteLine("Successfully initialized RabbitMQ infrastructure on retry.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "RabbitMQ connection is not available. Ensure RabbitMQ is running and accessible.", ex);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Publishes an event to RabbitMQ
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override async Task PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        // Ensure infrastructure is initialized
        await EnsureInitializedAsync();

        // Confirm Connection.
        if (!_rabbitMQConnection.IsConnected) await _rabbitMQConnection.TryConnectAsync(cancellationToken);

        // Create New Channel To Send the Message.
        using IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);

        // Create Exchange.
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

        // Get Basic Properties To Help Build The Publish Response.
        BasicProperties properties = CreateBasicProperties();

        // Build Payload.
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _defaultJsonSerializerOptions);

        // Send Message To The Objects Name.
        await channel.BasicPublishAsync(ExchangeName, @event.GetType().Name, false, properties, payload, cancellationToken: cancellationToken);

        // Close The Channel.
        await channel.CloseAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override async Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        // Ensure infrastructure is initialized
        await EnsureInitializedAsync();

        // Try Connect If Required.
        if (!_rabbitMQConnection.IsConnected) await _rabbitMQConnection.TryConnectAsync(cancellationToken);

        // Get T TypeName.
        string eventName = typeof(T).Name;

        // Create Channel And New Queue.
        using (IChannel _channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken))
        {
            // Bind The Queue To The Exchange.
            await _channel.QueueBindAsync(_queueName, ExchangeName, eventName, cancellationToken: cancellationToken);
        }

        // Add New Subscription.
        SubscriptionsManager.AddSubscription<T, TH>();

        // Assign Event Handler.
        await StartConsumeAsync(cancellationToken);
    }

    /// <summary>
    /// Starts consuming messages from the queue
    /// </summary>
    private async Task StartConsumeAsync(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);

        try
        {
            if (_consumerChannel == null)
            {
                Console.WriteLine("Warning: Consumer channel is not initialized. Skipping StartConsume.");
                return;
            }

            try
            {
                AsyncEventingBasicConsumer consumer = new(_consumerChannel);
                consumer.ReceivedAsync += Consumer_Received;
                await _consumerChannel.BasicConsumeAsync(_queueName, false, consumer, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting consume: {ex.Message}");
                _consumerChannel = await CreateConsumerChannelAsync(cancellationToken);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Handles received messages from RabbitMQ
    /// </summary>
    private async Task Consumer_Received(object sender, BasicDeliverEventArgs @event)
    {
        // Assign The Event Name.
        string? eventName = @event.RoutingKey;

        // Change The Routing Key To The Original And Try Again.
        if (@event.BasicProperties.Headers != null
            && @event.BasicProperties.IsHeadersPresent()
            && @event.BasicProperties.Headers.TryGetValue("x-chirp-event", out object? chirpEventName))
        {
            // Get The Original EventName.
            eventName = Encoding.UTF8.GetString(chirpEventName as byte[] ?? []);
        }

        string message = Encoding.UTF8.GetString(@event.Body.Span);
        int retries = 0;

        // Check The Headers For The Original Queue Name
        // And Also How Many Times Before This Message Has Been Sent.
        if (@event.BasicProperties.Headers != null
            && @event.BasicProperties.IsHeadersPresent()
            && @event.BasicProperties.Headers.TryGetValue("x-chirp-retry", out object? count) && count != null)
        {
            if (!int.TryParse(count.ToString(), out retries))
            {
                // Shouldn't Get Here.
                // If So, We Should Probs Push This Message To A Bucket.
            }
        }

        try
        {
            // Guard Clauses.
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new Exception("Event name is missing in the message.");
            }
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new Exception("Message body is empty.");
            }
            if (_consumerChannel == null)
            {
                throw new Exception("Consumer channel is not initialized.");
            }

            // Process Event.
            await ProcessEvent(eventName, message);

            // Acknowledge Message.
            await _consumerChannel.BasicAckAsync(@event.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            BasicProperties properties = CreateBasicProperties();

            // Headers
            properties.Headers = @event.BasicProperties.IsHeadersPresent()
                ? new Dictionary<string, object?>(@event.BasicProperties.Headers!)
                : new Dictionary<string, object?>();

            properties.Headers["x-chirp-event"] = eventName ?? "";
            properties.Headers["x-chirp-exception"] = ex.Message;

            // Check If We Should Retry.
            if (retries < _retryMax)
            {
                // Increment Retry Count.
                properties.Headers["x-chirp-retry"] = ++retries;

                // Consider removing this delay and using TTL retry queues instead
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, retries)));

                // IMPORTANT: publish then ack (at-least-once) so we don't lose messages if publish fails
                // And we dont have duplicate messages.
                await _consumerChannel!.BasicPublishAsync(
                    ExchangeName,
                    eventName ?? @event.RoutingKey,
                    mandatory: false,
                    properties,
                    @event.Body.ToArray());

                await _consumerChannel.BasicAckAsync(@event.DeliveryTag, multiple: false);
            }
            else
            {
                await PublishToDLXAsync(@event, properties, cancellationToken: @event.CancellationToken);

                // Make sure the message is not redelivered forever
                await _consumerChannel!.BasicAckAsync(@event.DeliveryTag, multiple: false);
            }
        }
    }

    /// <summary>
    /// Publishes a failed message to the dead letter exchange
    /// </summary>
    private async Task PublishToDLXAsync(BasicDeliverEventArgs @event, BasicProperties properties, CancellationToken cancellationToken = default)
    {
        // Push To A Bucket.
        // Create New Channel To Send the Message.
        using (IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken))
        {
            // Create Exchange.
            await channel.ExchangeDeclareAsync(DlxExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

            // Send Message To The Objects Name.
            await channel.BasicPublishAsync(DlxExchangeName, _dlxQueueName, mandatory: false, properties, @event.Body.Span.ToArray(), cancellationToken: cancellationToken);
        }

        // Tell The Consumer All Is Ok As We Don't Want The Same
        // Message In Two Places.
        await _consumerChannel.BasicAckAsync(@event.DeliveryTag, false, cancellationToken);
    }

    /// <summary>
    /// Processes an event by invoking the appropriate handlers
    /// </summary>
    private async Task ProcessEvent(string eventName, string message)
    {
        // Wait for EventHandlers.
        int counter = 0;

        // Wait For The Handlers To Be All Loaded.
        while (!SubscriptionsManager.HasSubscriptionsForEvent(eventName))
        {
            if (counter < _retryMax)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                counter++;
            }
            else
            {
                throw new Exception($"Unable To Get Subscription For ${eventName}");
            }
        }

        // Important fix: Use ServiceProvider directly first to try to find singleton/transient handlers
        // that might have been directly retrieved in tests
        IEnumerable<SubscriptionInfo> subscriptions = SubscriptionsManager.GetHandlersForEvent(eventName);

        // Process using both ServiceProvider and a new scope to ensure handlers are found
        // in various registration scenarios
        bool anyHandled = await ProcessHandlers(subscriptions, ServiceProvider, eventName, message);

        // If no handlers were found in the root provider, try with a scope
        if (!anyHandled)
        {
            await using AsyncServiceScope scope = ServiceProvider.CreateAsyncScope();
            await ProcessHandlers(subscriptions, scope.ServiceProvider, eventName, message);
        }
    }

    /// <summary>
    /// Process event handlers from a specific service provider
    /// </summary>
    private async Task<bool> ProcessHandlers(
        IEnumerable<SubscriptionInfo> subscriptions,
        IServiceProvider provider,
        string eventName,
        string message)
    {
        bool anyHandled = false;

        foreach (SubscriptionInfo subscription in subscriptions)
        {
            object? handler = provider.GetService(subscription.HandlerType);
            if (handler == null) continue;

            Type? eventType = SubscriptionsManager.GetEventTypeByName(eventName) 
                ?? throw new Exception($"Unable To Get EventType For {eventName}");
            
            object? integrationEvent = null;

            try
            {
                integrationEvent = JsonSerializer.Deserialize(message, eventType, _defaultJsonSerializerOptions);
            }
            catch (NotSupportedException)
            {
                integrationEvent = JsonSerializer.Deserialize(message, eventType);
            }

            if (integrationEvent == null)
            {
                throw new Exception($"Failed to deserialize message for event type {eventType.Name}");
            }

            Type concreteType = typeof(IChirpIntegrationEventHandler<>).MakeGenericType(eventType);
            var handleMethod = concreteType.GetMethod("Handle");

            if (handleMethod != null)
            {
                await Task.Yield();
                await (Task)handleMethod.Invoke(handler, new[] { integrationEvent })!;
                anyHandled = true;
            }
            else
            {
                throw new Exception($"Handle method not found on handler {handler.GetType().Name}");
            }
        }

        return anyHandled;
    }

    /// <summary>
    /// Creates a channel for consuming messages
    /// </summary>
    private async Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken = default)
    {
        // Ensure only one thread creates the channel at a time
        await _semaphoreSlim.WaitAsync(cancellationToken);
        
        try
        {
            // Confirm Connection.
            if (!_rabbitMQConnection.IsConnected)
            {
                await _rabbitMQConnection.TryConnectAsync(cancellationToken);
            }

            // Create Channel.
            IChannel channel = await _rabbitMQConnection.CreateChannelAsync(cancellationToken);

            // Declare Exchange.
            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, true, cancellationToken: cancellationToken);

            // Declare Queue.
            await channel.QueueDeclareAsync(_queueName, true, false, false, null, cancellationToken: cancellationToken);

            // Improved exception handling for channel callbacks
            channel.CallbackExceptionAsync += async (sender, ea) =>
            {
                try
                {
                    // Log the exception
                    Console.WriteLine($"RabbitMQ channel exception: {ea.Exception.Message}");                   
                    await _consumerChannel.DisposeAsync();

                    try
                    {
                        if (_consumerChannel?.IsOpen == true)
                        {
                            await _consumerChannel.CloseAsync();
                        }
                    }
                    catch (Exception disposeEx)
                    {
                        Console.WriteLine($"Error disposing channel: {disposeEx.Message}");
                    }

                    // Wait a brief moment before reconnecting
                    await Task.Delay(500);

                    // Try to connect and check if successful
                    if (!_rabbitMQConnection.IsConnected)
                    {
                        await _rabbitMQConnection.TryConnectAsync(cancellationToken);
                    }

                    // Only proceed if connection is established
                    if (_rabbitMQConnection.IsConnected)
                    {
                        _consumerChannel = await CreateConsumerChannelAsync();
                        await StartConsumeAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recreating consumer channel: {ex.Message}");
                }
            };

            return channel;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}