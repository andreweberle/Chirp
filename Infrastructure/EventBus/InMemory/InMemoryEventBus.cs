using System.Text.Json;
using System.Text.Json.Serialization;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class InMemoryEventBus(
    IChirpEventBusSubscriptionsManager subscriptionsManager, 
    IServiceProvider serviceProvider) 
    : EventBusBase(subscriptionsManager, serviceProvider)
{
    private const string BrokerName = "chirp_event_bus";
    private readonly string _queueName;
    private readonly string _dlxQueueName;
    private readonly int _retryMax;
    
    // Single lock for all infrastructure changes (initialization and consumer starting)
    private readonly SemaphoreSlim _infrastructureLock = new(1, 1);
    
    private bool _isInfrastructureInitialized;
    private bool _isConsumerStarted;
    private string ExchangeName { get; }
    private string DlxExchangeName { get; }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };
    
    public override async Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override async Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
