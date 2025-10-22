using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly JsonSerializerOptions s_caseInsensitiveOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve
    };

    private readonly string _dlxQueueName;
    private readonly string? _queueName;
    private readonly IChirpRabbitMqConnection _rabbitMQConnection;
    private readonly int _retryMax;
    private IModel? _consumerChannel;
    private readonly object _lockObject = new();
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
        string? queueName = null,
        int retryMax = 5,
        string exchangeName = BrokerName,
        string dlxExchangeName = "_dlxExchangeName")
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
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
            InitializeInfrastructure();
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize RabbitMQ infrastructure during startup: {ex.Message}");
            Console.WriteLine("The event bus will attempt to reconnect on first use.");
            // Don't throw - allow the application to start
            // Initialization will be retried on first Publish/Subscribe call
        }
    }

    /// <summary>
    /// Initializes the RabbitMQ infrastructure (exchanges, queues, channels)
    /// </summary>
    private void InitializeInfrastructure()
    {
        // Create consumer channel
        _consumerChannel = CreateConsumerChannel();

        // Create DLX infrastructure
        using IModel channel = _rabbitMQConnection.CreateModel();

        // Create Exchange.
        channel.ExchangeDeclare(DlxExchangeName, ExchangeType.Direct, true);

        // Create the Queue
        channel.QueueDeclare(_dlxQueueName, true, false, false, null);

        // Bind DLXQ To DLX
        channel.QueueBind(_dlxQueueName, DlxExchangeName, _dlxQueueName);
    }

    /// <summary>
    /// Ensures infrastructure is initialized, attempting to initialize if not already done
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lockObject)
        {
            if (_initialized) return;

            try
            {
                InitializeInfrastructure();
                _initialized = true;
                Console.WriteLine("Successfully initialized RabbitMQ infrastructure on retry.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "RabbitMQ connection is not available. Ensure RabbitMQ is running and accessible.", ex);
            }
        }
    }

    /// <summary>
    /// Publishes an event to RabbitMQ
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override void Publish(IntegrationEvent @event)
    {
        // Ensure infrastructure is initialized
        EnsureInitialized();

        // Confirm Connection.
        if (!_rabbitMQConnection.IsConnected) _rabbitMQConnection.TryConnect();

        // Create New Channel To Send the Message.
        using IModel channel = _rabbitMQConnection.CreateModel();

        // Create Exchange.
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, true);

        // Get Basic Properties To Help Build The Publish Response.
        IBasicProperties properties = channel.CreateBasicProperties();
        properties.DeliveryMode = 2;

        // Build Payload.
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _defaultJsonSerializerOptions);

        // Confirm The Message Has Been Sent.
        channel.ConfirmSelect();

        // Send Message To The Objects Name.
        channel.BasicPublish(ExchangeName, @event.GetType().Name, properties, payload);

        // Wait For The Message To Be Sent.
        channel.WaitForConfirmsOrDie();

        // Close The Channel.
        channel.Close();

        // Dispose Of The Channel.
        channel.Dispose();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override void Subscribe<T, TH>()
    {
// Ensure infrastructure is initialized
        EnsureInitialized();

        // Try Connect If Required.
        if (!_rabbitMQConnection.IsConnected) _rabbitMQConnection.TryConnect();

        // Get T TypeName.
        string eventName = typeof(T).Name;

        // Create Channel And New Queue.
        using (IModel _channel = _rabbitMQConnection.CreateModel())
        {
            // Bind The Queue To The Exchange.
            _channel.QueueBind(_queueName, ExchangeName, eventName);
        }

        // Add New Subscription.
        SubscriptionsManager.AddSubscription<T, TH>();

        // Assign Event Handler.
        StartConsume();
    }

    /// <summary>
    /// Starts consuming messages from the queue
    /// </summary>
    private void StartConsume()
    {
        lock (_lockObject)
        {
            if (_consumerChannel == null)
            {
                Console.WriteLine("Warning: Consumer channel is not initialized. Skipping StartConsume.");
                return;
            }

            try
            {
                AsyncEventingBasicConsumer consumer = new(_consumerChannel);
                consumer.Received += Consumer_Received;
                _consumerChannel.BasicConsume(_queueName, false, consumer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting consume: {ex.Message}");
                // Try to recreate channel on exception
                _consumerChannel = CreateConsumerChannel();
            }
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
        if (@event.BasicProperties.IsHeadersPresent()
            && @event.BasicProperties.Headers.TryGetValue("x-chirp-event", out object? chirpEventName))
            // Get The Original EventName.
            eventName = Encoding.UTF8.GetString(chirpEventName as byte[] ?? []);

        string message = Encoding.UTF8.GetString(@event.Body.Span);

        int retries = 0;

        // Check The Headers For The Original Queue Name
        // And Also How Many Times Before This Message Has Been Sent.
        if (@event.BasicProperties.IsHeadersPresent()
            && @event.BasicProperties.Headers.TryGetValue("x-chirp-retry", out object? count))
            if (!int.TryParse(count.ToString(), out retries))
            {
                // Shouldn't Get Here.
                // If So, We Should Probs Push This Message To A Bucket.
            }


        try
        {
            await ProcessEvent(eventName, message);
            _consumerChannel.BasicAck(@event.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            if (retries < _retryMax)
            {
                if (!@event.BasicProperties.IsHeadersPresent())
                {
                    // Create Headers
                    @event.BasicProperties = _consumerChannel.CreateBasicProperties();
                    @event.BasicProperties.DeliveryMode = 2;
                    @event.BasicProperties.Headers = new Dictionary<string, object>
                    {
                        { "x-chirp-retry", 1 },
                        { "x-chirp-event", eventName },
                        { "x-chirp-exception", ex.Message }
                    };
                }
                else
                {
                    @event.BasicProperties.Headers["x-chirp-retry"] = ++retries;
                }

                await Task.Delay(TimeSpan.FromSeconds(1) * retries);

                _consumerChannel.BasicAck(@event.DeliveryTag, false);
                _consumerChannel.BasicPublish(ExchangeName, eventName, @event.BasicProperties, @event.Body.ToArray());
            }
            else
            {
                // Assign The Latest Error.
                @event.BasicProperties.Headers["x-chirp-exception"] = ex.Message;
                PublishToDLX(@event);
            }
        }
    }

    /// <summary>
    /// Publishes a failed message to the dead letter exchange
    /// </summary>
    private void PublishToDLX(BasicDeliverEventArgs @event)
    {
        // Push To A Bucket.
        // Create New Channel To Send the Message.
        using (IModel channel = _rabbitMQConnection.CreateModel())
        {
            // Create Exchange.
            channel.ExchangeDeclare(DlxExchangeName, ExchangeType.Direct, true);

            // Send Message To The Objects Name.
            channel.BasicPublish(DlxExchangeName, _dlxQueueName, @event.BasicProperties, @event.Body.Span.ToArray());
        }

        // Tell The Consumer All Is Ok As We Don't Want The Same
        // Message In Two Places.
        _consumerChannel.BasicAck(@event.DeliveryTag, false);
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
            if (counter < _retryMax)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                counter++;
            }
            else
            {
                throw new Exception($"Unable To Get Subscription For ${eventName}");
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

            Type? eventType = SubscriptionsManager.GetEventTypeByName(eventName);
            if (eventType == null)
            {
                throw new Exception($"Unable To Get EventType For {eventName}");
            }

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
    private IModel CreateConsumerChannel()
    {
        lock (_lockObject)
        {
            if (!_rabbitMQConnection.IsConnected) _rabbitMQConnection.TryConnect();

            IModel channel = _rabbitMQConnection.CreateModel();

            channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, true);
            channel.QueueDeclare(_queueName, true, false, false, null);

            // Improved exception handling for channel callbacks
            channel.CallbackException += (sender, ea) =>
            {
                try
                {
                    Console.WriteLine($"RabbitMQ channel exception: {ea.Exception.Message}");

                    // Safely dispose the existing channel
                    IModel oldChannel = _consumerChannel;
                    _consumerChannel = null; // Prevent reuse

                    try
                    {
                        if (oldChannel?.IsOpen == true)
                        {
                            oldChannel.Close();
                        }

                        oldChannel?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        Console.WriteLine($"Error disposing channel: {disposeEx.Message}");
                    }

                    // Wait a brief moment before reconnecting
                    Task.Delay(500).Wait();

                    // Try to connect and check if successful
                    if (!_rabbitMQConnection.IsConnected)
                    {
                        _rabbitMQConnection.TryConnect();
                    }

                    // Only proceed if connection is established
                    if (_rabbitMQConnection.IsConnected)
                    {
                        _consumerChannel = CreateConsumerChannel();
                        StartConsume();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recreating consumer channel: {ex.Message}");
                }
            };

            return channel;
        }
    }
}