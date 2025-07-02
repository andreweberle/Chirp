namespace Chirp.Infrastructure.EventBus.Kafka;

/// <summary>
/// Interface for Kafka connection - Implement when adding Kafka support
/// </summary>
public interface IKafkaConnection
{
    /// <summary>
    /// Ensures the connection to Kafka is established
    /// </summary>
    void EnsureConnected();

    /// <summary>
    /// Sends a message to a Kafka topic
    /// </summary>
    /// <param name="topic">The topic to send the message to</param>
    /// <param name="key">The message key</param>
    /// <param name="message">The message content</param>
    /// <param name="headers">Optional message headers</param>
    void SendMessage(string topic, string key, string message, IDictionary<string, string>? headers = null);

    /// <summary>
    /// Configures a consumer to receive messages
    /// </summary>
    /// <param name="topics">The topics to consume from</param>
    /// <param name="groupId">The consumer group ID</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    void ConfigureConsumer(IEnumerable<string> topics, string groupId,
        Func<string, string, IDictionary<string, string>, Task> messageHandler);
}