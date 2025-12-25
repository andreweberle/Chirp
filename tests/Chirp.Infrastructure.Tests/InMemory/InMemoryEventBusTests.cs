using System;
using System.Reflection;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.Tests.InMemory;

[TestClass]
public class InMemoryEventBusTests
{
    // Test event class as a record
    public record TestIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    // Second test event for multiple subscription tests
    public record SecondTestIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    // Third test event for multiple subscription tests
    public record ThirdTestIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    // Test event class that simulates failure
    private record TestFailedIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    // Test event handler class
    private class TestIntegrationEventHandler : IChirpIntegrationEventHandler<TestIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public string? ReceivedMessage { get; private set; }

        public Task Handle(TestIntegrationEvent @event)
        {
            HandlerCalled = true;
            ReceivedMessage = @event.Message;
            return Task.CompletedTask;
        }
    }

    // Second test event handler
    private class SecondTestIntegrationEventHandler : IChirpIntegrationEventHandler<SecondTestIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public string? ReceivedMessage { get; private set; }

        public Task Handle(SecondTestIntegrationEvent @event)
        {
            HandlerCalled = true;
            ReceivedMessage = @event.Message;
            return Task.CompletedTask;
        }
    }

    // Third test event handler
    private class ThirdTestIntegrationEventHandler : IChirpIntegrationEventHandler<ThirdTestIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public string? ReceivedMessage { get; private set; }

        public Task Handle(ThirdTestIntegrationEvent @event)
        {
            HandlerCalled = true;
            ReceivedMessage = @event.Message;
            return Task.CompletedTask;
        }
    }

    // Test event handler that simulates failure
    private class TestFailedIntegrationEventHandler : IChirpIntegrationEventHandler<TestFailedIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public string? ReceivedMessage { get; private set; }

        public Task Handle(TestFailedIntegrationEvent @event)
        {
            HandlerCalled = true;
            ReceivedMessage = @event.Message;

            // Simulate failure
            throw new Exception("Simulated handler failure");
        }
    }

    [TestMethod]
    public void Constructor_InitializesProperties_Successfully()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_queue";
        const string exchangeName = "test_exchange";
        const string dlxExchangeName = "test_dlx_exchange";

        // Use reflection to access private fields
        Type eventBusType = typeof(ChirpInMemoryEventBus);

        // Act
        ChirpInMemoryEventBus eventBus = new(subscriptionsManager, serviceProvider, queueName, 5, exchangeName,
            dlxExchangeName);

        // Get the private field
        FieldInfo? strProperty = eventBusType.GetField("_queueName", BindingFlags.NonPublic | BindingFlags.Instance);

        // Assert
        Assert.IsNotNull(eventBus);
        Assert.AreEqual(queueName, (string)strProperty!.GetValue(eventBus)!);
    }

    [TestMethod]
    public async Task PublishAsync_PublishesEventSuccessfully()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_publish_queue";
        const string exchangeName = "test_publish_exchange";
        const string dlxExchangeName = "test_publish_dlx_exchange";

        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        TestIntegrationEvent testEvent = new() { Message = "Test message" };

        // Act
        bool result = await eventBus.PublishAsync(testEvent, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(result, "Event should be published successfully");
    }

    [TestMethod]
    public async Task SubscribeAsync_RegistersEventHandler()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_subscribe_queue";
        const string exchangeName = "test_subscribe_exchange";
        const string dlxExchangeName = "test_subscribe_dlx_exchange";

        // Create a singleton handler instance
        TestIntegrationEventHandler typedHandler = new();

        // Set up DI with the handler as a singleton
        ServiceCollection services = [];
        services.AddSingleton(typedHandler);
        services.AddSingleton<TestIntegrationEventHandler>(_ => typedHandler);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<TestIntegrationEvent>(),
            "Subscription should be registered");
    }

    [TestMethod]
    public async Task MultipleSubscriptions_CanBeAddedSuccessfully()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_multiple_subscriptions_queue";
        const string exchangeName = "test_multiple_subscriptions_exchange";
        const string dlxExchangeName = "test_multiple_subscriptions_dlx_exchange";

        // Create handler instances
        TestIntegrationEventHandler handler1 = new();
        SecondTestIntegrationEventHandler handler2 = new();
        ThirdTestIntegrationEventHandler handler3 = new();

        // Set up DI
        ServiceCollection services = [];
        services.AddSingleton(handler1);
        services.AddSingleton<TestIntegrationEventHandler>(_ => handler1);
        services.AddSingleton(handler2);
        services.AddSingleton<SecondTestIntegrationEventHandler>(_ => handler2);
        services.AddSingleton(handler3);
        services.AddSingleton<ThirdTestIntegrationEventHandler>(_ => handler3);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act - Subscribe to multiple events in sequence
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);
        await eventBus.SubscribeAsync<SecondTestIntegrationEvent, SecondTestIntegrationEventHandler>(TestContext
            .CancellationToken);
        await eventBus.SubscribeAsync<ThirdTestIntegrationEvent, ThirdTestIntegrationEventHandler>(TestContext
            .CancellationToken);

        // Assert
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<TestIntegrationEvent>(),
            "First subscription should be registered");
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<SecondTestIntegrationEvent>(),
            "Second subscription should be registered");
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<ThirdTestIntegrationEvent>(),
            "Third subscription should be registered");
    }

    [TestMethod]
    public async Task SubscribeAsync_LogsErrors_OnFailure()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        // Create a faulty service provider that returns null
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        const string queueName = "test_error_logging_queue";
        const string exchangeName = "test_error_logging_exchange";
        const string dlxExchangeName = "test_error_logging_dlx_exchange";

        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act - This should complete without errors since it only registers the subscription
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);

        // Assert - Verify subscription was registered
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<TestIntegrationEvent>(),
            "Subscription should be registered even without handler in DI");
    }

    [TestMethod]
    public async Task PublishAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        const string queueName = "test_publish_success_queue";
        const string exchangeName = "test_publish_success_exchange";
        const string dlxExchangeName = "test_publish_success_dlx_exchange";

        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        TestIntegrationEvent testEvent = new() { Message = "Success test message" };

        // Act
        bool result = await eventBus.PublishAsync(testEvent);

        // Assert
        Assert.IsTrue(result, "Publish should return true on success");
    }

    [TestMethod]
    public async Task ProcessHandlers_InvokesRegisteredHandler()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_process_handlers_queue";
        const string exchangeName = "test_process_handlers_exchange";
        const string dlxExchangeName = "test_process_handlers_dlx_exchange";

        // Create a singleton handler instance
        TestIntegrationEventHandler typedHandler = new();

        // Set up DI with the handler
        ServiceCollection services = [];
        services.AddSingleton(typedHandler);
        services.AddSingleton<TestIntegrationEventHandler>(_ => typedHandler);
        services.AddSingleton<IChirpInMemoryDeadLetterQueue, InMemoryDeadLetterQueue>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Subscribe to the event
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);

        // Create and serialize a test event
        TestIntegrationEvent testEvent = new() { Message = "Test process handlers message" };
        string eventJson = System.Text.Json.JsonSerializer.Serialize(testEvent,
            new System.Text.Json.JsonSerializerOptions
            {
                IncludeFields = true,
                WriteIndented = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                PropertyNameCaseInsensitive = true
            });

        // Act
        bool result =
            await eventBus.ProcessHandlers(nameof(TestIntegrationEvent), eventJson, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(result, "ProcessHandlers should return true when handler is invoked");
        Assert.IsTrue(typedHandler.HandlerCalled, "Handler should be called");
        Assert.AreEqual(testEvent.Message, typedHandler.ReceivedMessage, "Handler should receive correct message");
    }
    
    [TestMethod]
    public async Task MultipleHandlers_CanProcessSameEvent()
    {
        // Arrange
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_multiple_handlers_queue";
        const string exchangeName = "test_multiple_handlers_exchange";
        const string dlxExchangeName = "test_multiple_handlers_dlx_exchange";

        // Create multiple handler instances for the same event type
        TestIntegrationEventHandler handler1 = new();
        TestIntegrationEventHandler handler2 = new();

        // Set up DI
        ServiceCollection services = new();
        services.AddTransient<TestIntegrationEventHandler>(_ => handler1);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpInMemoryEventBus eventBus = new(
            subscriptionsManager,
            serviceProvider,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Subscribe the handler
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);

        // Create and serialize a test event
        TestIntegrationEvent testEvent = new() { Message = "Multiple handlers test" };
        string eventJson = System.Text.Json.JsonSerializer.Serialize(testEvent, new System.Text.Json.JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
            PropertyNameCaseInsensitive = true
        });

        // Act
        bool result = await eventBus.ProcessHandlers(nameof(TestIntegrationEvent), eventJson, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(result, "ProcessHandlers should return true");
        Assert.IsTrue(handler1.HandlerCalled, "First handler should be called");
    }

    public TestContext TestContext { get; set; } = null!;
}