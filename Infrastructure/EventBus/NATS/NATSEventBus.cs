using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.NATS;

/// <summary>
/// NATS implementation of the event bus.
/// </summary>
public class NATSEventBus : EventBusBase
{
    private readonly INATSConnection _natsConnection;
    private readonly string _subjectPrefix;
    private readonly string _queueGroup;
    private readonly Dictionary<string, string> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of NATSEventBus
    /// </summary>
    /// <param name="natsConnection">The NATS connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="subjectPrefix">The subject prefix</param>
    /// <param name="queueGroup">The queue group for load balancing (optional)</param>
    public NATSEventBus(
        INATSConnection natsConnection,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string subjectPrefix,
        string queueGroup = null)
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
        _natsConnection = natsConnection ?? throw new ArgumentNullException(nameof(natsConnection));
        _subjectPrefix = subjectPrefix ?? throw new ArgumentNullException(nameof(subjectPrefix));
        _queueGroup = queueGroup;
    }

    /// <summary>
    /// Publishes an event to NATS
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override void Publish(IntegrationEvent @event)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override void Subscribe<T, TH>()
    {
        throw new NotImplementedException();
    }

    private async Task HandleNATSMessageAsync(string subject, string message)
    {
        throw new NotImplementedException();
    }
}