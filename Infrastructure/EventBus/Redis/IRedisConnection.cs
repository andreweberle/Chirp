namespace Chirp.Infrastructure.EventBus.Redis;

/// <summary>
/// Interface for Redis Pub/Sub connection - Implement when adding Redis support
/// </summary>
public interface IRedisConnection
{
    /// <summary>
    /// Ensures the connection to Redis is established
    /// </summary>
    void EnsureConnected();

    /// <summary>
    /// Publishes a message to a Redis channel
    /// </summary>
    /// <param name="channel">The channel name</param>
    /// <param name="message">The message content</param>
    Task PublishAsync(string channel, string message);

    /// <summary>
    /// Subscribes to a Redis channel
    /// </summary>
    /// <param name="channel">The channel name</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    Task SubscribeAsync(string channel, Func<string, string, Task> messageHandler);

    /// <summary>
    /// Unsubscribes from a Redis channel
    /// </summary>
    /// <param name="channel">The channel name</param>
    Task UnsubscribeAsync(string channel);
}