namespace Chirp.Infrastructure.EventBus.NATS;

/// <summary>
/// Interface for NATS connection - Implement when adding NATS support
/// </summary>
public interface INATSConnection
{
    /// <summary>
    /// Ensures the connection to NATS is established
    /// </summary>
    void EnsureConnected();

    /// <summary>
    /// Publishes a message to a subject
    /// </summary>
    /// <param name="subject">The subject name</param>
    /// <param name="message">The message content</param>
    void Publish(string subject, string message);

    /// <summary>
    /// Subscribes to a subject
    /// </summary>
    /// <param name="subject">The subject name</param>
    /// <param name="queueGroup">Optional queue group for load balancing</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    /// <returns>A subscription identifier</returns>
    string Subscribe(string subject, string? queueGroup, Func<string, string, Task> messageHandler);

    /// <summary>
    /// Unsubscribes from a subject
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier</param>
    void Unsubscribe(string subscriptionId);
}