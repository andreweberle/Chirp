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
}