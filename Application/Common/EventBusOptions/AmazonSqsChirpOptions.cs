using Chirp.Domain.Common;
using System;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for Amazon SQS event bus
/// </summary>
public class AmazonSqsChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to AmazonSqs
    /// </summary>
    public AmazonSqsChirpOptions()
    {
        EventBusType = EventBusType.AmazonSqs;
    }

    /// <summary>
    /// SQS queue URL (if provided, overrides QueueName)
    /// </summary>
    public string QueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Dead letter queue URL
    /// </summary>
    public string DeadLetterQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of messages to retrieve in a single request
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Wait time in seconds for long polling
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Visibility timeout in seconds
    /// </summary>
    public int VisibilityTimeout { get; set; } = 30;

    /// <summary>
    /// Whether to automatically create queues if they don't exist
    /// </summary>
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Message retention period in seconds
    /// </summary>
    public int MessageRetentionPeriod { get; set; } = 345600; // 4 days

    /// <summary>
    /// Whether to enable SQS FIFO queues (requires special queue name ending with .fifo)
    /// </summary>
    public bool EnableFifo { get; set; } = false;

    /// <summary>
    /// Whether to enable content-based deduplication for FIFO queues
    /// </summary>
    public bool ContentBasedDeduplication { get; set; } = false;
}