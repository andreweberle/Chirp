using Chirp.Domain.Common;
using System;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for Redis Pub/Sub event bus
/// </summary>
public class RedisChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to Redis
    /// </summary>
    public RedisChirpOptions()
    {
        EventBusType = EventBusType.Redis;
    }

    /// <summary>
    /// Channel prefix for Redis Pub/Sub
    /// </summary>
    public string ChannelPrefix
    {
        get => QueueName;
        set => QueueName = value;
    }

    /// <summary>
    /// Whether to use Redis Streams instead of Pub/Sub
    /// </summary>
    public bool UseRedisStreams { get; set; } = false;

    /// <summary>
    /// Stream max length (only used when UseRedisStreams is true)
    /// </summary>
    public long? StreamMaxLength { get; set; } = 1000;

    /// <summary>
    /// Stream consumer group name (only used when UseRedisStreams is true)
    /// </summary>
    public string ConsumerGroupName { get; set; } = "chirp_consumer_group";

    /// <summary>
    /// Stream consumer name (only used when UseRedisStreams is true)
    /// </summary>
    public string ConsumerName { get; set; } = "chirp_consumer";

    /// <summary>
    /// Whether to use Redis key expiration
    /// </summary>
    public bool UseKeyExpiration { get; set; } = false;

    /// <summary>
    /// Key expiration time (only used when UseKeyExpiration is true)
    /// </summary>
    public TimeSpan KeyExpirationTime { get; set; } = TimeSpan.FromDays(1);
}