namespace Chirp.Infrastructure.EventBus.AmazonSQS;

/// <summary>
/// Interface for Amazon SQS connection - Implement when adding Amazon SQS support
/// </summary>
public interface IAmazonSQSConnection
{
    /// <summary>
    /// Sends a message to an SQS queue
    /// </summary>
    /// <param name="queueUrl">The queue URL</param>
    /// <param name="message">The message content</param>
    /// <param name="attributes">Optional message attributes</param>
    Task SendMessageAsync(string queueUrl, string message, IDictionary<string, string>? attributes = null);

    /// <summary>
    /// Starts message processing from a queue
    /// </summary>
    /// <param name="queueUrl">The queue URL</param>
    /// <param name="messageHandler">The handler for processing messages</param>
    Task StartMessageProcessingAsync(string queueUrl, Func<string, IDictionary<string, string>, Task> messageHandler);

    /// <summary>
    /// Stops message processing
    /// </summary>
    Task StopMessageProcessingAsync();
}