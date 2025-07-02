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
public class RedisEventBus : EventBusBase
{
    private readonly IRedisConnection _redisConnection;
    private readonly string _channelPrefix;
    private readonly Dictionary<string, bool> _subscriptions = new();

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of RedisEventBus
    /// </summary>
    /// <param name="redisConnection">The Redis connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="channelPrefix">The channel prefix</param>
    public RedisEventBus(
        IRedisConnection redisConnection,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string channelPrefix)
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
        _redisConnection = redisConnection ?? throw new ArgumentNullException(nameof(redisConnection));
        _channelPrefix = channelPrefix ?? throw new ArgumentNullException(nameof(channelPrefix));
    }

    /// <summary>
    /// Publishes an event to Redis
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override void Publish(IntegrationEvent @event)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override void Subscribe<T, TH>()
    {
        throw new NotImplementedException();
    }

    private async Task HandleRedisMessageAsync(string channel, string message)
    {
        throw new NotImplementedException();
    }
}