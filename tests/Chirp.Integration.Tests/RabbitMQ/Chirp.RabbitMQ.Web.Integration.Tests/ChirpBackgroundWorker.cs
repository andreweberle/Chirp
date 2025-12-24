using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.RabbitMQ.Web.Integration.Tests.IntegrationEvents;

namespace Chirp.RabbitMQ.Web.Integration.Tests;

public class ChirpBackgroundWorker(
    ILogger<ChirpBackgroundWorker> logger,
    IChirpEventBus chirpEventBus) : BackgroundService
{
    private readonly ILogger<ChirpBackgroundWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IChirpEventBus _chirpEventBus = chirpEventBus ?? throw new ArgumentNullException(nameof(chirpEventBus));
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for a random amount of time between 1 and 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 3)), stoppingToken);

            // Check if we have logging enabled
            if (_logger.IsEnabled(LogLevel.Information))
            {
                // Log the message
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            // Send a random message to the event bus
            bool result =  await _chirpEventBus.PublishAsync(
                new ChirpEvent($"Hello World! @ '{DateTimeOffset.Now}'"), stoppingToken);
            
            // Check if the message was published successfully
            if (result)
            {
                _logger.LogInformation("Message published successfully.");
            }
            else
            {
                _logger.LogError("Message publishing failed.");
            }
        }
    }
}