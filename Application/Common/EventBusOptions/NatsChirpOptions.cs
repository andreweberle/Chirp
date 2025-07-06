using Chirp.Domain.Common;
using System;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for NATS event bus
/// </summary>
public class NatsChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to NATS
    /// </summary>
    public NatsChirpOptions()
    {
        EventBusType = Infrastructure.EventBus.EventBusType.NATS;
    }

    /// <summary>
    /// Subject prefix for NATS messages
    /// </summary>
    public string SubjectPrefix
    {
        get => QueueName;
        set => QueueName = value;
    }

    /// <summary>
    /// Queue group for load balancing (optional)
    /// </summary>
    public string QueueGroup { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use NATS JetStream instead of Core NATS
    /// </summary>
    public bool UseJetStream { get; set; } = false;

    /// <summary>
    /// Stream name for JetStream (only used when UseJetStream is true)
    /// </summary>
    public string StreamName { get; set; } = "CHIRP_EVENTS";

    /// <summary>
    /// Whether to create the stream if it doesn't exist (only used when UseJetStream is true)
    /// </summary>
    public bool AutoCreateStream { get; set; } = true;

    /// <summary>
    /// Maximum age of messages in the stream
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Maximum number of messages in the stream
    /// </summary>
    public long MaxMsgs { get; set; } = 1_000_000;

    /// <summary>
    /// Maximum size of the stream in bytes
    /// </summary>
    public long MaxBytes { get; set; } = 1_073_741_824; // 1 GB

    /// <summary>
    /// Discard policy for when stream limits are reached ("old" or "new")
    /// </summary>
    public string DiscardPolicy { get; set; } = "old";
}