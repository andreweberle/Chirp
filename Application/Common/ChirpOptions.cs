namespace Chirp.Application.Common;

/// <summary>
/// Configuration options for Chirp event bus
/// </summary>
public class ChirpOptions
{
    /// <summary>
    /// Type of event bus to use
    /// </summary>
    public EventBusType EventBusType { get; set; }
    
    /// <summary>
    /// Whether to automatically subscribe to event handler types.
    /// </summary>
    public bool AutoSubscribeConsumers { get; set; } = true;

    /// <summary>
    /// Queue, topic, or channel name for the messaging service
    /// </summary>
    public string QueueName { get; set; } = "chirp_default_queue";

    /// <summary>
    /// Number of retries for processing events
    /// </summary>
    public int RetryCount { get; set; } = 5;
    
    /// <summary>
    /// Whether to enable logging
    /// </summary>
    public bool LoggingEnabled { get; set; } = false;

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
        where THandler : Chirp.Application.Interfaces.IIChirpIntegrationEventHandler
    {
        // Don't register the same handler type twice'
        if (Consumers.Any(c => c.HandlerType == typeof(THandler)))
        {
            return this;
        }

        // Register the handler type
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