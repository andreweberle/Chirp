using Chirp.Application.Interfaces;
using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

namespace Chirp.RabbitMQ.Web.Integration.Tests;

public class ChirpHandleDockerRabbitMqImageWorker(
    ILogger<ChirpHandleDockerRabbitMqImageWorker> logger,
    IChirpEventBus chirpEventBus) : BackgroundService
{
    private readonly ILogger<ChirpHandleDockerRabbitMqImageWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IChirpEventBus _chirpEventBus = chirpEventBus ?? throw new ArgumentNullException(nameof(chirpEventBus));
    
    private static RabbitMqContainer _rabbitmqContainer = null!;
    private static readonly string Username = "guest";
    private static readonly string Password = "guest";
    private static string _hostname = null!;
    private static int _port;
    private static int _webPort;
    
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _rabbitmqContainer.StopAsync(cancellationToken);
        await _rabbitmqContainer.DisposeAsync();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}