namespace Chirp.Infrastructure.EventBus.AzureServiceBus;

/// <summary>
/// Interface for Azure Service Bus connection - Implement when adding Azure Service Bus support
/// </summary>
public interface IAzureServiceBusConnection
{
    /// <summary>
    /// Ensures the connection to Azure Service Bus is established
    /// </summary>
    void EnsureConnected();

    /// <summary>
    /// Sends a message to a queue or topic
    /// </summary>
    /// <param name="queueOrTopicName">The queue or topic name</param>
    /// <param name="message">The message content</param>
    /// <param name="properties">Optional message properties</param>
    Task SendMessageAsync(string queueOrTopicName, string message, IDictionary<string, object>? properties = null);

    /// <summary>
    /// Registers a message handler for a queue or subscription
    /// </summary>
    /// <param name="queueOrSubscriptionName">The queue or subscription name</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    Task RegisterMessageHandlerAsync(string queueOrSubscriptionName,
        Func<string, IDictionary<string, object>, Task> messageHandler);
}