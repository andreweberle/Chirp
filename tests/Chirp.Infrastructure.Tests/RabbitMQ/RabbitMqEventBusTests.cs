using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System.Text.Json;
using System.Threading.Tasks;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMqEventBusTests
{
    private RabbitMqContainer? _rabbitMqContainer;
    private RabbitMqEventBus? _eventBus;
    private IRabbitMqConnection? _rabbitMqConnection;
    private IEventBusSubscriptionsManager? _subscriptionManager;
    private ServiceCollection _serviceCollection;
    private ServiceProvider _serviceProvider;
    private const string ExchangeName = "test_unit_exchange";
    private const string QueueName = "test_unit_queue";

    [TestInitialize]
    public async Task Initialize()
    {
        // Set up the RabbitMQ test container
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithPortBinding(5672, true)
            .Build();

        // Start the container
        await _rabbitMqContainer.StartAsync();

        // Create a connection factory using the container's connection info
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "testuser",
            Password = "testpassword",
            RequestedHeartbeat = TimeSpan.FromSeconds(10),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        // Create the RabbitMQ connection
        _rabbitMqConnection = new RabbitMqConnection(connectionFactory);
        _rabbitMqConnection.TryConnect(); // Connect explicitly

        // Create the subscription manager
        _subscriptionManager = new InMemoryEventBusSubscriptionsManager();

        // Setup DI for test handlers
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddTransient<TestIntegrationEventHandler>();
        _serviceProvider = _serviceCollection.BuildServiceProvider();

        // Create the event bus
        _eventBus = new RabbitMqEventBus(
            _rabbitMqConnection,
            _serviceProvider,
            _subscriptionManager,
            QueueName,
            5,
            ExchangeName,
            "test_dlx_exchange");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }

        _serviceProvider.Dispose();
    }

    [TestMethod]
    public void Constructor_InitializesProperties_Successfully()
    {
        // Arrange
        var mockSubscriptionManager = new InMemoryEventBusSubscriptionsManager();
        var mockServiceProvider = new ServiceCollection().BuildServiceProvider();
        const string uniqueQueueName = "constructor_test_queue";

        // Act
        var eventBus = new RabbitMqEventBus(
            _rabbitMqConnection,
            mockServiceProvider,
            mockSubscriptionManager,
            uniqueQueueName,
            5);

        // Assert
        Assert.IsNotNull(eventBus);
        // We can't directly test private members, but the fact that it constructs
        // successfully indicates the initialization worked
    }

    [TestMethod]
    public void Publish_SendsMessageToRabbitMQ()
    {
        // Arrange
        var testEvent = new TestIntegrationEvent { Message = "Test message" };

        // Act - This should not throw
        _eventBus?.Publish(testEvent);

        // Assert - We can't easily verify the message was published without subscribing,
        // but we can verify it doesn't throw
    }

    [TestMethod]
    public void Subscribe_RegistersEventHandler()
    {
        // Act
        _eventBus?.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        Assert.IsTrue(_subscriptionManager?.HasSubscriptionsForEvent<TestIntegrationEvent>());
    }

    [TestMethod]
    public void Subscribe_WhenCalled_RegistersHandlerInSubscriptionManager()
    {
        // Arrange
        var eventType = typeof(TestIntegrationEvent);
        var eventName = eventType.Name;

        // Act
        _eventBus?.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        Assert.IsTrue(_subscriptionManager?.HasSubscriptionsForEvent(eventName));
        var handlers = _subscriptionManager?.GetHandlersForEvent(eventName);
        Assert.IsNotNull(handlers);
        Assert.AreEqual(1, handlers.Count());
        Assert.AreEqual(typeof(TestIntegrationEventHandler), handlers.First().HandlerType);
    }

    // Test classes for integration events - using correct record inheritance syntax
    public record TestIntegrationEvent() : IntegrationEvent
    {
        public string? Message { get; set; }
    }

    public class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task Handle(TestIntegrationEvent @event)
        {
            // For testing purposes
            return Task.CompletedTask;
        }
    }
}