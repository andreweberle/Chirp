using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.InMemory;

public class ChirpInMemoryProcessor(
    ILogger<ChirpInMemoryProcessor> logger, 
    Channel<IntegrationEvent> channel,
    IChirpEventBus chirpEventBus) : BackgroundService
{
    private readonly ILogger<ChirpInMemoryProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Channel<IntegrationEvent> _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    private readonly IChirpEventBus _chirpEventBus = chirpEventBus ?? throw new ArgumentNullException(nameof(chirpEventBus));
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken: stoppingToken))
        {
            while (_channel.Reader.TryRead(out IntegrationEvent? integrationEvent))
            {
                // Get the event name.
                string eventName = integrationEvent.GetType().Name;
                
                // Serialize the event to JSON.
                string message = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());
                
                int retryCount = 0;
                
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
                    Console.WriteLine($"Error processing event {eventName}: {ex.Message}");
                }
            }
        }
    }
}