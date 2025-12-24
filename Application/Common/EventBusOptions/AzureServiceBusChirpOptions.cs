using Chirp.Domain.Common;
using System;
using System.Collections.Generic;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for Azure Service Bus event bus
/// </summary>
public class AzureServiceBusChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to AzureServiceBus
    /// </summary>
    public AzureServiceBusChirpOptions()
    {
        EventBusType = EventBusType.AzureServiceBus;
    }

    /// <summary>
    /// Whether to use topics instead of queues
    /// </summary>
    public bool UseTopics { get; set; } = false;

    /// <summary>
    /// Topic name (only used when UseTopics is true)
    /// </summary>
    public string TopicName
    {
        get => QueueName;
        set => QueueName = value;
    }

    /// <summary>
    /// Subscription name (only used when UseTopics is true)
    /// </summary>
    public string SubscriptionName { get; set; } = "chirp_subscription";

    /// <summary>
    /// Max concurrent calls for processing messages
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 10;

    /// <summary>
    /// Whether to enable sessions
    /// </summary>
    public bool EnableSessions { get; set; } = false;

    /// <summary>
    /// Dead letter queue path
    /// </summary>
    public string DeadLetterQueuePath { get; set; } = "$DeadLetterQueue";

    /// <summary>
    /// Whether to auto-create resources if they don't exist
    /// </summary>
    public bool AutoCreateResources { get; set; } = true;

    /// <summary>
    /// Message time to live
    /// </summary>
    public TimeSpan MessageTimeToLive { get; set; } = TimeSpan.FromDays(14);
}