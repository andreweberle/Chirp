using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class ChirpInMemoryEventBus : EventBusBase
{
    private readonly string _exchangeName;
    private readonly string _dlxExchangeName;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };
    
    private const string BrokerName = "chirp_event_bus";
    private readonly string _queueName;
    private readonly string _dlxQueueName;
    public int RetryCount { get; }
    
    private readonly IChirpEventBusSubscriptionsManager _subscriptionsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChirpLogger> _logger;
    
    private bool _isConsumerStarted;
    private readonly IServiceProvider _serviceProvider1;

    public ChirpInMemoryEventBus(IChirpEventBusSubscriptionsManager subscriptionsManager,
        IServiceProvider serviceProvider,
        string queueName,
        int retryMax = 5,
        string exchangeName = ChirpInMemoryEventBus.BrokerName,
        string dlxExchangeName = "_dlxExchangeName") : base(retryMax, subscriptionsManager, serviceProvider)
    {
        _serviceProvider1 = serviceProvider;
        _exchangeName = exchangeName;
        _dlxExchangeName = dlxExchangeName;
        _queueName = queueName;
        _dlxQueueName = $"{queueName}_dlx";
        RetryCount = retryMax;
        _subscriptionsManager = subscriptionsManager;
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<ChirpLogger>>();
        
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("InMemory event bus created. Infrastructure will be initialized on first use.");
    }

    public override async Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the channel
            Channel<IntegrationEvent> channel = _serviceProvider1.GetRequiredService<Channel<IntegrationEvent>>();
            
            // Check if the channel is null
            if (channel == null)
            {
                if (_logger.IsEnabled(LogLevel.Error)) _logger.LogError("Channel is null. Ensure the channel is properly initialized.");
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

            if (_logger.IsEnabled(LogLevel.Information))
            {
                // Log subscription
                _logger.LogInformation($"--------------");
                _logger.LogInformation("Attempting to subscribe {typeof(TH).Name} to {eventName}", typeof(TH).Name, eventName);   
            }

            // Register subscription
            SubscriptionsManager.AddSubscription<T, TH>();

            // Mark consumer as started
            this._isConsumerStarted = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                // Log subscription
                _logger.LogInformation("Subscribed {typeof(TH).Name} to {eventName}", typeof(TH).Name, eventName);
            }
        }
        catch (Exception ex)
        {
            if (!_logger.IsEnabled(LogLevel.Error)) return;
            _logger.LogError(ex, "Error subscribing {typeof(TH).Name}: {Message}", typeof(TH).Name, ex.Message);
            throw;
        }
    }
}
