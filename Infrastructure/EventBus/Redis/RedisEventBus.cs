using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.Redis;

/// <summary>
/// Redis Pub/Sub implementation of the event bus.
/// </summary>
/// <remarks>
/// Initializes a new instance of RedisEventBus
/// </remarks>
/// <param name="redisConnection">The Redis connection</param>
/// <param name="serviceProvider">The service provider for resolving handlers</param>
/// <param name="eventBusSubscriptionsManager">The subscription manager</param>
/// <param name="channelPrefix">The channel prefix</param>
public class RedisEventBus(
    IRedisConnection redisConnection,
    IServiceProvider serviceProvider,
    IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
    string channelPrefix) : EventBusBase(eventBusSubscriptionsManager, serviceProvider)
{
    private readonly IRedisConnection _redisConnection = redisConnection ?? throw new ArgumentNullException(nameof(redisConnection));
    private readonly string _channelPrefix = channelPrefix ?? throw new ArgumentNullException(nameof(channelPrefix));
    private readonly Dictionary<string, bool> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Publishes an event to Redis
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task HandleRedisMessageAsync(string channel, string message)
    {
        throw new NotImplementedException();
    }
}