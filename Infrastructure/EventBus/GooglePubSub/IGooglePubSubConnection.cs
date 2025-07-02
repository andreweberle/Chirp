namespace Chirp.Infrastructure.EventBus.GooglePubSub;

/// <summary>
/// Interface for Google Cloud Pub/Sub connection - Implement when adding Google Cloud Pub/Sub support
/// </summary>
public interface IGooglePubSubConnection
{
    /// <summary>
    /// Publishes a message to a topic
    /// </summary>
    /// <param name="topicId">The topic ID</param>
    /// <param name="message">The message content</param>
    /// <param name="attributes">Optional message attributes</param>
    Task PublishAsync(string topicId, string message, IDictionary<string, string>? attributes = null);

    /// <summary>
    /// Creates a subscription to a topic
    /// </summary>
    /// <param name="topicId">The topic ID</param>
    /// <param name="subscriptionId">The subscription ID</param>
    Task CreateSubscriptionAsync(string topicId, string subscriptionId);

    /// <summary>
    /// Starts message processing from a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    Task StartProcessingAsync(string subscriptionId, Func<string, IDictionary<string, string>, Task> messageHandler);

    /// <summary>
    /// Stops message processing for a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    Task StopProcessingAsync(string subscriptionId);
}