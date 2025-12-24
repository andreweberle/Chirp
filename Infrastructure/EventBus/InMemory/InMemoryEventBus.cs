using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class InMemoryEventBus(
    IChirpEventBusSubscriptionsManager subscriptionsManager, 
    IServiceProvider serviceProvider) 
    : EventBusBase(subscriptionsManager, serviceProvider)
{
    public override Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
