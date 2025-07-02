using Chirp.Application.Interfaces;
using Chirp.Domain.Common;

namespace Chirp.Infrastructure.EventBus.Common;

/// <summary>
/// In-memory implementation of IEventBusSubscriptionsManager
/// </summary>
public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
{
    private readonly List<Type> _eventTypes = [];
    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers = [];

    public event EventHandler<string>? OnEventRemoved;
    public bool IsEmpty => _handlers is { Count: 0 };

    public void Clear()
    {
        _handlers.Clear();
    }

    public void AddSubscription<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        string eventName = GetEventKey<T>();

        DoAddSubscription(typeof(TH), eventName, false);

        if (!_eventTypes.Contains(typeof(T))) _eventTypes.Add(typeof(T));
    }

    public void RemoveSubscription<T, TH>()
        where TH : IIntegrationEventHandler<T>
        where T : IntegrationEvent
    {
        SubscriptionInfo? handlerToRemove = FindSubscriptionToRemove<T, TH>();
        if (handlerToRemove == null) return;
        string eventName = GetEventKey<T>();
        DoRemoveHandler(eventName, handlerToRemove);
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
    {
        string key = GetEventKey<T>();
        return GetHandlersForEvent(key);
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName)
    {
        return _handlers[eventName];
    }

    public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
    {
        string key = GetEventKey<T>();
        return HasSubscriptionsForEvent(key);
    }

    public bool HasSubscriptionsForEvent(string eventName)
    {
        return _handlers.ContainsKey(eventName);
    }

    public Type? GetEventTypeByName(string eventName)
    {
        return _eventTypes.SingleOrDefault(t => t.Name == eventName);
    }

    public string GetEventKey<T>()
    {
        return typeof(T).Name;
    }

    private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
    {
        if (!HasSubscriptionsForEvent(eventName)) _handlers.Add(eventName, []);

        if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            throw new ArgumentException(
                $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));

        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
    }

    private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
    {
        _handlers[eventName].Remove(subsToRemove);
        if (_handlers[eventName].Count != 0) return;
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

    private SubscriptionInfo? FindSubscriptionToRemove<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        string eventName = GetEventKey<T>();
        return DoFindSubscriptionToRemove(eventName, typeof(TH));
    }

    private SubscriptionInfo? DoFindSubscriptionToRemove(string eventName, Type handlerType)
    {
        return !HasSubscriptionsForEvent(eventName)
            ? null
            : _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);
    }
}