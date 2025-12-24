using Chirp.Application.Interfaces;
using Chirp.RabbitMQ.Web.Integration.Tests.IntegrationEvents;

namespace Chirp.RabbitMQ.Web.Integration.Tests.Handlers;


public class NewMessageEventHandler(ILogger<NewMessageEventHandler> logger) : IChirpIntegrationEventHandler<ChirpEvent>
{
    private readonly ILogger<NewMessageEventHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task Handle(ChirpEvent @event)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Log the event message
            _logger.LogInformation("Received event: {@Event}", @event);
        }

        // Simulate a long running with random delay
        await Task.Delay(TimeSpan.FromSeconds(new Random().Next(1, 10)));
    }
}