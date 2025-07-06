using Chirp.Domain.Common;
using System;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for Google Cloud Pub/Sub event bus
/// </summary>
public class GooglePubSubChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to GooglePubSub
    /// </summary>
    public GooglePubSubChirpOptions()
    {
        EventBusType = Infrastructure.EventBus.EventBusType.GooglePubSub;
    }

    /// <summary>
    /// Google Cloud project ID
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Topic prefix for Google Pub/Sub
    /// </summary>
    public string TopicPrefix
    {
        get => QueueName;
        set => QueueName = value;
    }

    /// <summary>
    /// Subscription ID prefix
    /// </summary>
    public string SubscriptionIdPrefix { get; set; } = "chirp-subscription";

    /// <summary>
    /// Whether to automatically create topics if they don't exist
    /// </summary>
    public bool AutoCreateTopics { get; set; } = true;

    /// <summary>
    /// Whether to automatically create subscriptions if they don't exist
    /// </summary>
    public bool AutoCreateSubscriptions { get; set; } = true;

    /// <summary>
    /// Message acknowledgement deadline in seconds
    /// </summary>
    public int AcknowledgeDeadlineSeconds { get; set; } = 60;

    /// <summary>
    /// Message retention duration
    /// </summary>
    public TimeSpan MessageRetentionDuration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to enable message ordering
    /// </summary>
    public bool EnableMessageOrdering { get; set; } = false;

    /// <summary>
    /// Whether to retain acknowledged messages
    /// </summary>
    public bool RetainAckedMessages { get; set; } = false;
}