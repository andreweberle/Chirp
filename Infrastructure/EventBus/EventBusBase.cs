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
    int retryMax,
    IServiceProvider serviceProvider)
    : IChirpEventBus
{
    private readonly IServiceProvider ServiceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true
    };

    public int RetryMax { get; } = retryMax;

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
        // Create a scope to ensure
        await using AsyncServiceScope scope = ServiceProvider.CreateAsyncScope(); 
        
        // Get the SubscriptionManager from DI
        IChirpEventBusSubscriptionsManager subscriptionsManager = scope.ServiceProvider.GetRequiredService<IChirpEventBusSubscriptionsManager>();
        
        // Get the event type
        bool anyHandled = false;
        
        // Get the event type
        Type? eventType = null;

        // Retry logic
        int currentRetry = 0;
        
        // Keep trying until we get the event type or max retries exceeded
        while (eventType == null)
        {
            // Check if max retries exceeded
            if (currentRetry > RetryMax)
            {
                // Throw exception if max retries exceeded
                throw new Exception($"Could not find event type for message: {message} after {RetryMax} retries.");
            }
            
            // Get event type
            eventType = subscriptionsManager.GetEventTypeByName(eventName);
            
            // If found, break out of loop immediately.
            if (eventType != null) break;
            
            // If not found, wait and retry
            currentRetry++;
            
            // Wait before retrying
            // We will wait for 1 second multiplied by the current retry attempt.
            await Task.Delay(1000 * currentRetry, cancellationToken);
        }

        // Deserialize the event
        object? integrationEvent = JsonSerializer.Deserialize(message, eventType, JsonOptions);

        // Check if deserialization succeeded
        if (integrationEvent == null) return false;
        
        // Process each subscription
        foreach (SubscriptionInfo subscription in subscriptionsManager.GetHandlersForEvent(eventName))
        {
            // Resolve the handler
            object? handler = null;

            // Retry logic
            int handlerRetry = 0;
            while (handler == null)
            {
                // Check if max retries exceeded
                if (handlerRetry > RetryMax)
                {
                    throw new Exception($"Could not resolve handler for subscription: {subscription}");
                }

                // Attempt to resolve the handler
                handler = scope.ServiceProvider.GetService(subscription.HandlerType);

                // If resolved, break out of loop immediately.
                if (handler != null) break;

                // If not resolved, wait and retry
                handlerRetry++;

                // Wait before retrying
                await Task.Delay(1000 * handlerRetry, cancellationToken);
            }

            // Create the concrete handler type
            Type concreteType = typeof(IChirpIntegrationEventHandler<>).MakeGenericType(eventType);

            // Get the Handle method
            MethodInfo? method = concreteType.GetMethod("Handle");

                // If method not found, skip
                if (method == null)
                {
                    throw new Exception($"Could not find Handle method for integration event: {eventType.Name}");
                }
                
                // Invoke the handler
                await (Task)method.Invoke(handler, [integrationEvent])!;

                // Mark as handled
                anyHandled = true;
        }

        // Return whether any handler processed the event
        return anyHandled; 
    }
}