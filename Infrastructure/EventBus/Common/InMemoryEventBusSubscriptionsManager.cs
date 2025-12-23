using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.Common;

/// <summary>
/// In-memory implementation of IEventBusSubscriptionsManager
/// </summary>
public class InMemoryEventBusSubscriptionsManager : IChirpEventBusSubscriptionsManager
{
    private readonly List<Type> _eventTypes = [];
    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers = [];
    private readonly Lock _lock = new();

    public event EventHandler<string>? OnEventRemoved;
    
    public bool IsEmpty 
    {
        get 
        {
            using (_lock.EnterScope())
            {
                return _handlers.Count == 0;
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            _handlers.Clear();
        }
    }

    public void AddSubscription<T, TH>()
        where T : IntegrationEvent
        where TH : IChirpIntegrationEventHandler<T>
    {
        string eventName = GetEventKey<T>();

        using (_lock.EnterScope())
        {
            DoAddSubscription(typeof(TH), eventName, false);

            if (!_eventTypes.Contains(typeof(T))) _eventTypes.Add(typeof(T));
        }
    }

    public void RemoveSubscription<T, TH>()
        where TH : IChirpIntegrationEventHandler<T>
        where T : IntegrationEvent
    {
        string eventName = GetEventKey<T>();
        using (_lock.EnterScope())
        {
            SubscriptionInfo? handlerToRemove = DoFindSubscriptionToRemove(eventName, typeof(TH));
            if (handlerToRemove == null) return;
            
            DoRemoveHandler(eventName, handlerToRemove);
        }
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
    {
        string key = GetEventKey<T>();
        return GetHandlersForEvent(key);
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName)
    {
        using (_lock.EnterScope())
        {
            if (_handlers.TryGetValue(eventName, out var handlers))
            {
                return handlers.ToList();
            }
            return [];
        }
    }

    public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
    {
        string key = GetEventKey<T>();
        return HasSubscriptionsForEvent(key);
    }

    public bool HasSubscriptionsForEvent(string eventName)
    {
        using (_lock.EnterScope())
        {
            return _handlers.ContainsKey(eventName);
        }
    }

    public Type? GetEventTypeByName(string eventName)
    {
        using (_lock.EnterScope())
        {
            return _eventTypes.SingleOrDefault(t => t.Name == eventName);
        }
    }

    public string GetEventKey<T>()
    {
        return typeof(T).Name;
    }

    private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
    {
        if (!_handlers.ContainsKey(eventName)) _handlers.Add(eventName, []);

        if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            throw new ArgumentException(
                $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));

        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
    }

    private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers)) return;
        
        handlers.Remove(subsToRemove);
        if (handlers.Count != 0) return;
        
        _handlers.Remove(eventName);
        Type? eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
        if (eventType != null) _eventTypes.Remove(eventType);

        RaiseOnEventRemoved(eventName);
    }

    private void RaiseOnEventRemoved(string eventName)
    {
        EventHandler<string>? handler = OnEventRemoved;
        handler?.Invoke(this, eventName);
    }

    private SubscriptionInfo? DoFindSubscriptionToRemove(string eventName, Type handlerType)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers)) return null;
        return handlers.SingleOrDefault(s => s.HandlerType == handlerType);
    }
}