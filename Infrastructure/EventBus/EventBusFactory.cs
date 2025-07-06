using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Chirp.Infrastructure.EventBus.Kafka;
using Chirp.Infrastructure.EventBus.AzureServiceBus;
using Chirp.Infrastructure.EventBus.AmazonSQS;
using Chirp.Infrastructure.EventBus.Redis;
using Chirp.Infrastructure.EventBus.GooglePubSub;
using Chirp.Infrastructure.EventBus.NATS;
using Microsoft.Extensions.Configuration;

namespace Chirp.Infrastructure.EventBus;

/// <summary>
/// Supported event bus types
/// </summary>
public enum EventBusType
{
    /// <summary>
    /// RabbitMQ implementation
    /// </summary>
    RabbitMQ,

    /// <summary>
    /// Apache Kafka implementation
    /// </summary>
    Kafka,

    /// <summary>
    /// Azure Service Bus implementation
    /// </summary>
    AzureServiceBus,

    /// <summary>
    /// Amazon Simple Queue Service implementation
    /// </summary>
    AmazonSqs,

    /// <summary>
    /// Redis Pub/Sub implementation
    /// </summary>
    Redis,

    /// <summary>
    /// Google Cloud Pub/Sub implementation
    /// </summary>
    GooglePubSub,

    /// <summary>
    /// NATS messaging system implementation
    /// </summary>
    NATS
}

/// <summary>
/// Factory for creating event bus instances
/// </summary>
public static class EventBusFactory
{
    /// <summary>
    /// Creates an event bus implementation based on the specified type
    /// </summary>
    /// <param name="eventBusType">The type of event bus to create</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="queueName">The queue name</param>
    /// <param name="retryCount">Number of retries for processing events</param>
    /// <returns>An event bus implementation</returns>
    public static IChirpEventBus Create(
        EventBusType eventBusType,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string queueName,
        int retryCount = 5)
    {
        // Create a subscription manager to be used by the event bus
        IChirpEventBusSubscriptionsManager subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        return eventBusType switch
        {
            EventBusType.RabbitMQ => CreateRabbitMQEventBus(
                serviceProvider,
                subscriptionsManager,
                configuration,
                queueName,
                retryCount),

            EventBusType.Kafka => throw new NotImplementedException(
                "Kafka event bus is not implemented yet."),

            EventBusType.AzureServiceBus => throw new NotImplementedException(
                "Azure Service Bus implementation is not available yet."),

            EventBusType.AmazonSqs => throw new NotImplementedException(
                "Amazon SQS implementation is not available yet."),

            EventBusType.Redis => throw new NotImplementedException(
                "Redis Pub/Sub implementation is not available yet."),

            EventBusType.GooglePubSub => throw new NotImplementedException(
                "Google Cloud Pub/Sub implementation is not available yet."),

            EventBusType.NATS => throw new NotImplementedException(
                "NATS implementation is not available yet."),

            _ => throw new ArgumentException($"Unsupported event bus type: {eventBusType}")
        };
    }

    private static ChirpRabbitMqEventBus CreateRabbitMQEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string queueName,
        int retryCount)
    {
        // Get the RabbitMQ connection
        IChirpRabbitMqConnection connection = serviceProvider.GetService(typeof(IChirpRabbitMqConnection)) as IChirpRabbitMqConnection
                                         ?? throw new InvalidOperationException(
                                             "RabbitMQ connection not registered in service provider");

        // Get exchange names from configuration (with defaults)
        string exchangeName = configuration["RMQ:ExchangeName"] ?? "lithoconnect_event_bus";
        string dlxExchangeName = configuration["RMQ:ExchangeNameDLX"] ?? "_dlxExchangeName";

        // Create the RabbitMQ event bus
        return new ChirpRabbitMqEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            retryCount,
            exchangeName,
            dlxExchangeName);
    }


    // Example implementation for when Kafka is ready
    private static KafkaEventBus CreateKafkaEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string topicName,
        int retryCount)
    {
        // Get the Kafka connection
        IKafkaConnection connection = serviceProvider.GetService(typeof(IKafkaConnection)) as IKafkaConnection
                                      ?? throw new InvalidOperationException(
                                          "Kafka connection not registered in service provider");

        // Create the Kafka event bus
        return new KafkaEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            topicName,
            retryCount);
    }

    // Example implementation for Azure Service Bus
    private static AzureServiceBusEventBus CreateAzureServiceBusEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string queueName,
        int retryCount)
    {
        // Get the Azure Service Bus connection
        IAzureServiceBusConnection connection =
            serviceProvider.GetService(typeof(IAzureServiceBusConnection)) as IAzureServiceBusConnection
            ?? throw new InvalidOperationException("Azure Service Bus connection not registered in service provider");

        // Create the Azure Service Bus event bus
        return new AzureServiceBusEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            retryCount);
    }

    // Example implementation for Amazon SQS
    private static AmazonSqsEventBus CreateAmazonSQSEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string queueName,
        int retryCount)
    {
        // Get the Amazon SQS connection
        IAmazonSQSConnection connection =
            serviceProvider.GetService(typeof(IAmazonSQSConnection)) as IAmazonSQSConnection
            ?? throw new InvalidOperationException("Amazon SQS connection not registered in service provider");

        string deadLetterQueueUrl = configuration["AWS:DeadLetterQueueUrl"]
                                    ?? throw new ArgumentNullException(
                                        "AWS:DeadLetterQueueUrl configuration is missing");

        // Create the Amazon SQS event bus
        return new AmazonSqsEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            deadLetterQueueUrl,
            retryCount);
    }

    // Example implementation for Redis
    private static RedisEventBus CreateRedisEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string channelPrefix,
        int retryCount)
    {
        // Get the Redis connection
        IRedisConnection connection = serviceProvider.GetService(typeof(IRedisConnection)) as IRedisConnection
                                      ?? throw new InvalidOperationException(
                                          "Redis connection not registered in service provider");

        // Create the Redis event bus
        return new RedisEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            channelPrefix);
    }

    // Example implementation for Google Cloud Pub/Sub
    private static GooglePubSubEventBus CreateGooglePubSubEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string topicPrefix,
        int retryCount)
    {
        // Get the Google Pub/Sub connection
        IGooglePubSubConnection connection =
            serviceProvider.GetService(typeof(IGooglePubSubConnection)) as IGooglePubSubConnection
            ?? throw new InvalidOperationException("Google Pub/Sub connection not registered in service provider");

        string subscriptionIdPrefix = configuration["GoogleCloud:SubscriptionIdPrefix"]
                                      ?? configuration["GoogleCloud:ProjectId"]
                                      ?? throw new ArgumentNullException(
                                          "GoogleCloud:SubscriptionIdPrefix configuration is missing");

        // Create the Google Pub/Sub event bus
        return new GooglePubSubEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            topicPrefix,
            subscriptionIdPrefix);
    }

    // Example implementation for NATS
    private static NATSEventBus CreateNATSEventBus(
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager subscriptionsManager,
        IConfiguration configuration,
        string subjectPrefix,
        int retryCount)
    {
        // Get the NATS connection
        INATSConnection connection = serviceProvider.GetService(typeof(INATSConnection)) as INATSConnection
                                     ?? throw new InvalidOperationException(
                                         "NATS connection not registered in service provider");

        string queueGroup = configuration["NATS:QueueGroup"] ?? "";

        // Create the NATS event bus
        return new NATSEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            subjectPrefix,
            queueGroup);
    }
}