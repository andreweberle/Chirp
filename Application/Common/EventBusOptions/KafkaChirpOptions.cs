using Chirp.Domain.Common;
using System.Collections.Generic;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for Kafka event bus
/// </summary>
public class KafkaChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to Kafka
    /// </summary>
    public KafkaChirpOptions()
    {
        EventBusType = Infrastructure.EventBus.EventBusType.Kafka;
    }

    /// <summary>
    /// Kafka topic name (overrides QueueName from base class)
    /// </summary>
    public string TopicName
    {
        get => QueueName;
        set => QueueName = value;
    }

    /// <summary>
    /// Consumer group ID for Kafka consumer
    /// </summary>
    public string ConsumerGroupId { get; set; } = "chirp_consumer_group";

    /// <summary>
    /// Whether to automatically create topics if they don't exist
    /// </summary>
    public bool AutoCreateTopics { get; set; } = true;

    /// <summary>
    /// Number of partitions for auto-created topics
    /// </summary>
    public int NumPartitions { get; set; } = 1;

    /// <summary>
    /// Replication factor for auto-created topics
    /// </summary>
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// Additional Kafka producer configuration
    /// </summary>
    public Dictionary<string, string> ProducerConfig { get; set; } = new();

    /// <summary>
    /// Additional Kafka consumer configuration
    /// </summary>
    public Dictionary<string, string> ConsumerConfig { get; set; } = new();
}