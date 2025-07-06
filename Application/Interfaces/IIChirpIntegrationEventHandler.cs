using Chirp.Domain.Common;

namespace Chirp.Application.Interfaces;

public interface IIChirpIntegrationEventHandler
{
}

public interface IChirpIntegrationEventHandler<in TIntegrationEvent> : IIChirpIntegrationEventHandler
    where TIntegrationEvent : IntegrationEvent
{
    Task Handle(TIntegrationEvent @event);
}