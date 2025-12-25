using Chirp.Domain.Common;

namespace Chirp.Application.Interfaces;

public interface IChirpEventBus
{
    /// <summary>
    /// </summary>
    /// <param name="event"></param>
    public Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TH"></typeparam>
    public Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
        where T : IntegrationEvent
        where TH : IChirpIntegrationEventHandler<T>;

    public Task<bool> ProcessHandlers(string eventName, string message, CancellationToken cancellationToken = default);
}