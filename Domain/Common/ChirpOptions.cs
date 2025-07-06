using Chirp.Infrastructure.EventBus;


namespace Chirp.Domain.Common;

/// <summary>
/// Configuration options for Chirp event bus
/// </summary>
public class ChirpOptions
{
    /// <summary>
    /// Type of event bus to use
    /// </summary>
    public EventBusType EventBusType { get; set; } = EventBusType.RabbitMQ;

    /// <summary>
    /// Queue, topic, or channel name for the messaging service
    /// </summary>
    public string QueueName { get; set; } = "chirp_default_queue";

    /// <summary>
    /// Number of retries for processing events
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Collection of registered event handler types
    /// </summary>
    internal List<ConsumerRegistration> Consumers { get; } = [];

    /// <summary>
    /// Registers an integration event handler (consumer) to process events
    /// </summary>
    /// <typeparam name="THandler">The event handler type to register</typeparam>
    /// <returns>The ChirpOptions instance for fluent configuration</returns>
    public ChirpOptions AddConsumer<THandler>()
        where THandler : class
    {
        Consumers.Add(new ConsumerRegistration(typeof(THandler)));
        return this;
    }
}

/// <summary>
/// Represents a registered event handler/consumer
/// </summary>
internal class ConsumerRegistration(Type handlerType)
{
    /// <summary>
    /// The event handler/consumer type
    /// </summary>
    public Type HandlerType { get; } = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
}