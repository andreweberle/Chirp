using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus;

/// <summary>
/// Abstract base class for event bus implementations to share common functionality
/// </summary>
public abstract class EventBusBase(
    IChirpEventBusSubscriptionsManager subscriptionsManager,
    IServiceProvider serviceProvider)
    : IChirpEventBus
{
    protected readonly IChirpEventBusSubscriptionsManager SubscriptionsManager =
        subscriptionsManager ?? throw new ArgumentNullException(nameof(subscriptionsManager));

    protected readonly IServiceProvider ServiceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Publishes an event to the event bus
    /// </summary>
    /// <param name="event">The event to publish</param>
    public abstract Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public abstract Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
        where T : IntegrationEvent
        where TH : IChirpIntegrationEventHandler<T>;
}