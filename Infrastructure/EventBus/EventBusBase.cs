using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus;

/// <summary>
/// Abstract base class for event bus implementations to share common functionality
/// </summary>
public abstract class EventBusBase(
    IChirpEventBusSubscriptionsManager subscriptionsManager,
    IServiceProvider serviceProvider)
    : IChirpEventBus
{
    protected readonly IChirpEventBusSubscriptionsManager SubscriptionsManager =
        subscriptionsManager ?? throw new ArgumentNullException(nameof(subscriptionsManager));

    protected readonly IServiceProvider ServiceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };  

    /// <summary>
    /// Publishes an event to the event bus
    /// </summary>
    /// <param name="event">The event to publish</param>
    public abstract Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public abstract Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
        where T : IntegrationEvent
        where TH : IChirpIntegrationEventHandler<T>;
    
    /// <summary>
    /// Processes all handlers for the specified event
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ProcessHandlers(string eventName, string message, CancellationToken cancellationToken = default)
    {
        // Get the event type
        bool anyHandled = false;

        // Get the event type
        Type? eventType = SubscriptionsManager.GetEventTypeByName(eventName);

        // Check if event type found
        if (eventType == null) return false;

        // Deserialize the event
        object? integrationEvent = JsonSerializer.Deserialize(message, eventType, _jsonOptions);

        // Check if deserialization succeeded
        if (integrationEvent == null) return false;

        // Process each subscription
        foreach (SubscriptionInfo subscription in SubscriptionsManager.GetHandlersForEvent(eventName))
        {
            // Resolve the handler
            object? handler = ServiceProvider.GetService(subscription.HandlerType);

            // If handler not found, skip
            if (handler == null) continue;

            // Create the concrete handler type
            Type concreteType = typeof(IChirpIntegrationEventHandler<>).MakeGenericType(eventType);

            // Get the Handle method
            MethodInfo? method = concreteType.GetMethod("Handle");

            // If method not found, skip
            if (method == null) continue;
            
            // Invoke the handler
            await (Task)method.Invoke(handler, [integrationEvent])!;

            // Mark as handled
            anyHandled = true;
        }

        // Return whether any handler processed the event
        return anyHandled; 
    }
}