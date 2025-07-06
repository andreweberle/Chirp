using Chirp.Domain.Common;
using System;

namespace Chirp.Application.Common.EventBusOptions;

/// <summary>
/// Configuration options for RabbitMQ event bus
/// </summary>
public class RabbitMqChirpOptions : ChirpOptions
{
    /// <summary>
    /// Constructor that sets EventBusType to RabbitMQ
    /// </summary>
    public RabbitMqChirpOptions()
    {
        EventBusType = Infrastructure.EventBus.EventBusType.RabbitMQ;
    }

    /// <summary>
    /// The RabbitMQ host to connect to
    /// </summary>
    /// <remarks>
    /// When specified, this value overrides the "RMQ:Host" configuration setting
    /// </remarks>
    public string? Host { get; set; }

    /// <summary>
    /// The username for RabbitMQ authentication
    /// </summary>
    /// <remarks>
    /// When specified, this value overrides the "RMQ:Username" configuration setting
    /// </remarks>
    public string? Username { get; set; }

    /// <summary>
    /// The password for RabbitMQ authentication
    /// </summary>
    /// <remarks>
    /// When specified, this value overrides the "RMQ:Password" configuration setting
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    /// Exchange name for RabbitMQ messages
    /// </summary>
    public string ExchangeName { get; set; } = "chirp_event_bus";

    /// <summary>
    /// Dead letter exchange name for failed messages
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "chirp_dlx_exchange";

    /// <summary>
    /// Whether to automatically create the exchange if it doesn't exist
    /// </summary>
    public bool AutoCreateExchange { get; set; } = true;

    /// <summary>
    /// Whether to automatically create the queue if it doesn't exist
    /// </summary>
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Whether the queue should be durable (persist even after broker restart)
    /// </summary>
    public bool QueueDurable { get; set; } = true;

    /// <summary>
    /// Whether messages should be persistent
    /// </summary>
    public bool PersistentMessages { get; set; } = true;
}