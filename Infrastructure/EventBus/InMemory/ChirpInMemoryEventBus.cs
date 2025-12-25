using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class ChirpInMemoryEventBus(
    IChirpEventBusSubscriptionsManager subscriptionsManager,
    IServiceProvider serviceProvider,
    string queueName,
    int retryMax = 5,
    string exchangeName = ChirpInMemoryEventBus.BrokerName,
    string dlxExchangeName = "_dlxExchangeName")
    : EventBusBase(retryMax, subscriptionsManager, serviceProvider)
{
    private readonly string _exchangeName = exchangeName;
    private readonly string _dlxExchangeName = dlxExchangeName;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };
    
    private const string BrokerName = "chirp_event_bus";
    private readonly string _queueName = queueName;
    private readonly string _dlxQueueName = $"{queueName}_dlx";
    public int RetryCount { get; } = retryMax;
    
    private readonly IChirpEventBusSubscriptionsManager _subscriptionsManager = subscriptionsManager;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    
    private bool _isConsumerStarted;

    public override async Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the channel
            Channel<IntegrationEvent> channel = serviceProvider.GetRequiredService<Channel<IntegrationEvent>>();
            
            // Check if the channel is null
            if (channel == null)
            {
                throw new InvalidOperationException("Channel is null. Ensure the channel is properly initialized.");
            }
            
            // Publish the event
            await channel.Writer.WriteAsync(@event, cancellationToken);
            
            // Log published event.
            return true;
        }
        catch (InvalidOperationException e)
        {
            // Log the error.
            Console.WriteLine(e);
            
            // Swallow the exception as the users application should not fail due to event bus issues.
            
            // TODO: Create a Global Error Event Handler So The User Can Handle Event Bus Errors.

            return false;
        }
    }

    public override async Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        
        try
        {
            // Get the event name.
            string eventName = typeof(T).Name;

            // Log subscription
            Console.WriteLine($"--------------");
            Console.WriteLine($"Attempting to subscribe {typeof(TH).Name} to {eventName}");

            // Register subscription
            SubscriptionsManager.AddSubscription<T, TH>();

            // Mark consumer as started
            this._isConsumerStarted = true;

            // Log subscription
            Console.WriteLine($"Subscribed {typeof(TH).Name} to {eventName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing {typeof(TH).Name}: {ex.Message}");
            throw;
        }
    }
}
