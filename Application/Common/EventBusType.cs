namespace Chirp.Application.Common;

/// <summary>
/// Supported event bus types
/// </summary>
public enum EventBusType
{
    /// <summary>
    /// In-memory implementation
    /// </summary>
    InMemory = 0,
    
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