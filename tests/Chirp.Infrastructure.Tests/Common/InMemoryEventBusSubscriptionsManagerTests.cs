using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus.Common;

namespace Chirp.Infrastructure.Tests.Common;

[TestClass]
public class InMemoryEventBusSubscriptionsManagerTests
{
    [TestMethod]
    public void IsEmpty_InitialState_ReturnsTrue()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();

        // Assert
        Assert.IsTrue(manager.IsEmpty);
    }

    [TestMethod]
    public void AddSubscription_AddsHandlerForEvent()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();

        // Act
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        Assert.IsFalse(manager.IsEmpty);
        Assert.IsTrue(manager.HasSubscriptionsForEvent<TestIntegrationEvent>());
        Assert.IsTrue(manager.HasSubscriptionsForEvent(manager.GetEventKey<TestIntegrationEvent>()));
    }

    [TestMethod]
    public void GetHandlersForEvent_ReturnsCorrectHandler()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Act
        IEnumerable<SubscriptionInfo> handlers = manager.GetHandlersForEvent<TestIntegrationEvent>();

        // Assert
        IEnumerable<SubscriptionInfo> subscriptionInfos = handlers as SubscriptionInfo[] ?? handlers.ToArray();
        Assert.AreEqual(1, subscriptionInfos.Count());
        Assert.AreEqual(typeof(TestIntegrationEventHandler), subscriptionInfos.First().HandlerType);
    }

    [TestMethod]
    public void RemoveSubscription_RemovesHandlerForEvent()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Act
        manager.RemoveSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        Assert.IsTrue(manager.IsEmpty);
        Assert.IsFalse(manager.HasSubscriptionsForEvent<TestIntegrationEvent>());
    }

    [TestMethod]
    public void GetEventTypeByName_ReturnsCorrectType()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();
        string eventName = manager.GetEventKey<TestIntegrationEvent>();

        // Act
        Type? eventType = manager.GetEventTypeByName(eventName);

        // Assert
        Assert.IsNotNull(eventType);
        Assert.AreEqual(typeof(TestIntegrationEvent), eventType);
    }

    [TestMethod]
    public void GetEventTypeByName_NonExistentEventName_ReturnsNull()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();

        // Act
        Type? eventType = manager.GetEventTypeByName("NonExistentEventName");

        // Assert
        Assert.IsNull(eventType);
    }

    [TestMethod]
    public void Clear_RemovesAllSubscriptions()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();
        manager.AddSubscription<AnotherTestIntegrationEvent, AnotherTestIntegrationEventHandler>();

        // Act
        manager.Clear();

        // Assert
        Assert.IsTrue(manager.IsEmpty);
    }

    [TestMethod]
    public void AddMultipleSubscriptions_ForSameEvent_RegistersBoth()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();

        // Act
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();
        manager.AddSubscription<TestIntegrationEvent, AnotherHandlerForTestEvent>();

        // Assert
        IEnumerable<SubscriptionInfo> handlers = manager.GetHandlersForEvent<TestIntegrationEvent>();
        Assert.AreEqual(2, handlers.Count());
    }

    [TestMethod]
    public void RemoveOne_OfMultipleSubscriptions_LeavesOthers()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager manager = new InMemoryEventBusSubscriptionsManager();
        manager.AddSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();
        manager.AddSubscription<TestIntegrationEvent, AnotherHandlerForTestEvent>();

        // Act
        manager.RemoveSubscription<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        IEnumerable<SubscriptionInfo> handlers = manager.GetHandlersForEvent<TestIntegrationEvent>();
        Assert.AreEqual(1, handlers.Count());
        Assert.AreEqual(typeof(AnotherHandlerForTestEvent), handlers.First().HandlerType);
    }

    // Test event classes for testing
    private record TestIntegrationEvent : IntegrationEvent;

    private record AnotherTestIntegrationEvent : IntegrationEvent;

    // Test event handlers for testing
    private class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task Handle(TestIntegrationEvent @event)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherTestIntegrationEventHandler : IIntegrationEventHandler<AnotherTestIntegrationEvent>
    {
        public Task Handle(AnotherTestIntegrationEvent @event)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherHandlerForTestEvent : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task Handle(TestIntegrationEvent @event)
        {
            return Task.CompletedTask;
        }
    }
}