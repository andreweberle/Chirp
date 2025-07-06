using Chirp.Domain.Common;

namespace Chirp.Application.Interfaces;

public interface IChirpEventBus
{
    /// <summary>
    /// </summary>
    /// <param name="event"></param>
    public void Publish(IntegrationEvent @event);

    /// <summary>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TH"></typeparam>
    public void Subscribe<T, TH>()
        where T : IntegrationEvent
        where TH : IChirpIntegrationEventHandler<T>;
}