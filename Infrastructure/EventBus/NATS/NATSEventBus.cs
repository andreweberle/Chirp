using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.NATS;

/// <summary>
/// NATS implementation of the event bus.
/// </summary>
/// <remarks>
/// Initializes a new instance of NATSEventBus
/// </remarks>
/// <param name="natsConnection">The NATS connection</param>
/// <param name="serviceProvider">The service provider for resolving handlers</param>
/// <param name="eventBusSubscriptionsManager">The subscription manager</param>
/// <param name="subjectPrefix">The subject prefix</param>
/// <param name="queueGroup">The queue group for load balancing (optional)</param>
public class NATSEventBus(
    INATSConnection natsConnection,
    IServiceProvider serviceProvider,
    IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
    string subjectPrefix,
    string queueGroup = null) : EventBusBase(0, eventBusSubscriptionsManager, serviceProvider)
{
    private readonly INATSConnection _natsConnection = natsConnection ?? throw new ArgumentNullException(nameof(natsConnection));
    private readonly string _subjectPrefix = subjectPrefix ?? throw new ArgumentNullException(nameof(subjectPrefix));
    private readonly string _queueGroup = queueGroup;
    private readonly Dictionary<string, string> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Publishes an event to NATS
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

    private async Task HandleNATSMessageAsync(string subject, string message)
    {
        throw new NotImplementedException();
    }
}