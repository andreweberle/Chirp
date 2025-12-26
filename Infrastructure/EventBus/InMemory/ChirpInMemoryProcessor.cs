using System.Text.Json;
using System.Threading.Channels;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class ChirpInMemoryProcessor(
    ILogger<ChirpLogger> logger, 
    Channel<IntegrationEvent> channel,
    IChirpEventBus chirpEventBus,
    IChirpInMemoryDeadLetterQueue deadLetterQueue) : BackgroundService
{
    private readonly ILogger<ChirpLogger> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Channel<IntegrationEvent> _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    private readonly IChirpEventBus _chirpEventBus = chirpEventBus ?? throw new ArgumentNullException(nameof(chirpEventBus));
    private readonly IChirpInMemoryDeadLetterQueue _deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken: stoppingToken))
        {
            while (_channel.Reader.TryRead(out IntegrationEvent? integrationEvent))
            {
                // Get the event name.
                string eventName = integrationEvent.GetType().Name;
                
                // Get the event type.
                Type eventType = integrationEvent.GetType();
                
                // Serialize the event to JSON.
                string message = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());
                
                //  
                int retryCount = 0;
                
                // Check if this is a message is in the dead letter queue.
                if (_deadLetterQueue.TryGet(integrationEvent.Id, out ChirpInMemoryDeadLetterEnvelope? deadLetterEvent))
                {
                    retryCount = deadLetterEvent.RetryCount;
                }

                try
                {
                    // Process the event
                    await _chirpEventBus.ProcessHandlers(eventName, message, stoppingToken);

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Event {eventName} processed successfully", eventName);
                    }
                }
                catch (Exception ex)
                {
                    // Increment the retry count.
                    retryCount++;

                    // Log the error
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error processing event {eventName}", eventName);
                    }
                    
                    // Wait for a random amount of time between 1 and 3 seconds
                    await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 3)), stoppingToken);
                    
                    // Move the event to the dead letter queue.
                    _deadLetterQueue.EnqueueDeadLetter(new ChirpInMemoryDeadLetterEnvelope(integrationEvent, retryCount, ex));
                    
                    // Re-enqueue the event if we haven't reached the max retry count.
                    if (retryCount < _chirpEventBus.RetryMax)
                    {                        
                        await _channel.Writer.WriteAsync(integrationEvent, stoppingToken);
                    }
                    else
                    {
                        // Log the warning
                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError("Event {eventName} moved to the dead letter queue", eventName);
                        }

                        break;
                    }
                }
            }
        }
    }
}