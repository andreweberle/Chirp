using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.GooglePubSub;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.GooglePubSub;

/// <summary>
/// Google Cloud Pub/Sub implementation of the event bus.
/// </summary>
public class GooglePubSubEventBus : EventBusBase
{
    private readonly IGooglePubSubConnection _pubSubConnection;
    private readonly string _topicPrefix;
    private readonly string _subscriptionIdPrefix;
    private readonly Dictionary<string, bool> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of GooglePubSubEventBus
    /// </summary>
    /// <param name="pubSubConnection">The Google Cloud Pub/Sub connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="topicPrefix">The topic prefix</param>
    /// <param name="subscriptionIdPrefix">The subscription ID prefix</param>
    public GooglePubSubEventBus(
        IGooglePubSubConnection pubSubConnection,
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string topicPrefix,
        string subscriptionIdPrefix)
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
        _pubSubConnection = pubSubConnection ?? throw new ArgumentNullException(nameof(pubSubConnection));
        _topicPrefix = topicPrefix ?? throw new ArgumentNullException(nameof(topicPrefix));
        _subscriptionIdPrefix = subscriptionIdPrefix ?? throw new ArgumentNullException(nameof(subscriptionIdPrefix));
    }

    /// <summary>
    /// Publishes an event to Google Cloud Pub/Sub
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

    private async Task ProcessMessageAsync(string message, IDictionary<string, string> attributes)
    {
        throw new NotImplementedException();
    }
}