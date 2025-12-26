using Chirp.Application.Interfaces;
using Chirp.InMemory.Web.Integration.Tests.IntegrationEvents;

namespace Chirp.InMemory.Web.Integration.Tests.Handlers;


public class NewMessageEventHandler(ILogger<NewMessageEventHandler> logger) : IChirpIntegrationEventHandler<ChirpEvent>
{
    private readonly ILogger<NewMessageEventHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task Handle(ChirpEvent @event)
    {
        // Check if the user has logging enabled.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Log the event message
            _logger.LogInformation("Received event: {@Event}", @event);
        }

        // Simulate a random exception, we will use this to test error handling.
        // 1 in 3 chance of throwing an exception.
        if (@event.ThrowException && new Random().Next(1, 3) == 1)
        {
            throw new Exception("Test exception");
        }
    }
}