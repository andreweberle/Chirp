using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.AzureServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of the event bus.
/// </summary>
public class AzureServiceBusEventBus : EventBusBase
{
    private readonly IAzureServiceBusConnection _serviceBusConnection;
    private readonly int _retryMax;
    private readonly string _topicOrQueueName;
    private readonly Dictionary<string, string> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of AzureServiceBusEventBus
    /// </summary>
    /// <param name="serviceBusConnection">The Azure Service Bus connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="topicOrQueueName">The topic or queue name</param>
    /// <param name="retryMax">Maximum number of retries</param>
    public AzureServiceBusEventBus(
        IAzureServiceBusConnection serviceBusConnection,
        IServiceProvider serviceProvider,
        IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string topicOrQueueName,
        int retryMax = 5)
        : base(retryMax, serviceProvider, eventBusSubscriptionsManager)
    {
        _serviceBusConnection = serviceBusConnection ?? throw new ArgumentNullException(nameof(serviceBusConnection));
        _topicOrQueueName = topicOrQueueName ?? throw new ArgumentNullException(nameof(topicOrQueueName));
        _retryMax = retryMax;
    }

    /// <summary>
    /// Publishes an event to Azure Service Bus
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

    private async Task ProcessMessageAsync(string message, IDictionary<string, object> properties)
    {
        throw new NotImplementedException();
    }
}