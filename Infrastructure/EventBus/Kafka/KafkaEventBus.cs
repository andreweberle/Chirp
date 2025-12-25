using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.Kafka;

/// <summary>
/// Kafka implementation of the event bus.
/// </summary>
public class KafkaEventBus : EventBusBase
{
    private readonly IKafkaConnection _kafkaConnection;
    private readonly string _topic;
    private readonly int _retryMax;

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of KafkaEventBus
    /// </summary>
    /// <param name="kafkaConnection">The Kafka connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="topic">The Kafka topic</param>
    /// <param name="retryMax">Maximum number of retries</param>
    public KafkaEventBus(
        IKafkaConnection kafkaConnection,
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string topic,
        int retryMax = 5)
        : base(retryMax, eventBusSubscriptionsManager, serviceProvider)
    {
        _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _retryMax = retryMax;
    }

    /// <summary>
    /// Publishes an event to Kafka
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task ProcessMessageAsync(string key, string message, IDictionary<string, string> headers)
    {
        throw new NotImplementedException();
    }
}