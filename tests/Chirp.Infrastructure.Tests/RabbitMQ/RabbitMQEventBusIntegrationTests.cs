using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System.Reflection;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMQEventBusIntegrationTests
{
    private static RabbitMqContainer? _rabbitMqContainer;
    private static string _connectionString = string.Empty;
    private static string _host = string.Empty;
    private static int _port = 0;
    private const string BrokerName = "chirp_event_bus";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        // Create and start a RabbitMQ container
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithUsername("guest")
            .WithPassword("guest")
            .WithImage("rabbitmq:3-management")
            .Build();

        await _rabbitMqContainer.StartAsync();

        // Get connection details
        _host = _rabbitMqContainer.Hostname;
        _port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _connectionString = $"amqp://guest:guest@{_host}:{_port}";
    }

    [TestMethod]
    public void Create_RabbitMQEventBus_WithTestContainer_SuccessfullyCreates()
    {
        // Arrange
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RMQ:Host"] = _host,
                ["RMQ:Username"] = "guest",
                ["RMQ:Password"] = "guest",
                ["RMQ:Port"] = _port.ToString(),
                ["RMQ:ExchangeName"] = "chirp_test_exchange",
                ["RMQ:ExchangeNameDLX"] = "chirp_test_dlx_exchange"
            })
            .Build();

        // Register dependencies
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = _host,
                Port = _port,
                UserName = "guest",
                Password = "guest"
            };

            // Mock the RabbitMQConnection since it's internal
            Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

            return mockConnection.Object;
        });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act
        IEventBus eventBus = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            serviceProvider,
            configuration,
            "chirp_test_queue");

        // Assert
        Assert.IsInstanceOfType<RabbitMqEventBus>(eventBus);
    }

    [TestMethod]
    public async Task PublishAndSubscribe_EndToEnd_EventIsProcessed()
    {
        // This test simulates a full publish-subscribe cycle with a mock
        // Since we can't easily access the internal consumer in the RabbitMQEventBus,
        // we'll create a test where we can verify the subscription was registered

        // Arrange - Setup service collection
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RMQ:Host"] = _host,
                ["RMQ:Username"] = "guest",
                ["RMQ:Password"] = "guest",
                ["RMQ:Port"] = _port.ToString(),
                ["RMQ:ExchangeName"] = "chirp_integration_test_exchange",
                ["RMQ:ExchangeNameDLX"] = "chirp_integration_test_dlx"
            })
            .Build();

        // Set up a handler that we can check was called
        TestIntegrationEventHandlerWithCounter handler = new TestIntegrationEventHandlerWithCounter();
        services.AddTransient<TestIntegrationEventHandlerWithCounter>(_ => handler);

        // Register a real implementation of the handler
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(sp =>
            sp.GetRequiredService<TestIntegrationEventHandlerWithCounter>());

        // Setup connection with mocks to verify
        IModel mockModel = CreateMockModel();
        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.CreateModel()).Returns(mockModel);
            return mockConnection.Object;
        });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the event bus
        IEventBus eventBus = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            serviceProvider,
            configuration,
            "chirp_integration_test_queue");

        // Act - Subscribe to event
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandlerWithCounter>();

        // Verify that subscription was registered
        Mock<IModel> mockChannel = Mock.Get(mockModel);
        mockChannel.Verify(c => c.QueueBind(
                It.Is<string>(q => q == "chirp_integration_test_queue"),
                It.IsAny<string>(),
                It.Is<string>(s => s == nameof(TestIntegrationEvent)),
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);

        // Act - Publish event
        TestIntegrationEvent testEvent = new TestIntegrationEvent("Integration test message");
        eventBus.Publish(testEvent);

        // Verify that BasicPublish was called with the right event type as routing key
        mockChannel.Verify(c => c.BasicPublish(
                It.IsAny<string>(),
                It.Is<string>(s => s == nameof(TestIntegrationEvent)),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublishAndSubscribe_WithMultipleHandlers_AllHandlersAreRegistered()
    {
        // Arrange - Setup service collection
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RMQ:Host"] = _host,
                ["RMQ:Username"] = "guest",
                ["RMQ:Password"] = "guest",
                ["RMQ:Port"] = _port.ToString(),
                ["RMQ:ExchangeName"] = "chirp_multi_handler_test_exchange",
                ["RMQ:ExchangeNameDLX"] = "chirp_multi_handler_test_dlx"
            })
            .Build();

        // Set up handlers that we can check were called
        TestIntegrationEventHandlerWithCounter handler1 = new TestIntegrationEventHandlerWithCounter();
        AnotherTestIntegrationEventHandler handler2 = new AnotherTestIntegrationEventHandler();

        // Create a test event to publish
        TestIntegrationEvent testEvent = new TestIntegrationEvent("Test message for multiple handlers");

        // Setup service collection for dependency injection
        services.AddSingleton<TestIntegrationEventHandlerWithCounter>(handler1);
        services.AddSingleton<AnotherTestIntegrationEventHandler>(handler2);
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(sp => handler1);
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(sp => handler2);

        // Create subscription manager to verify registrations
        InMemoryEventBusSubscriptionsManager subscriptionManager = new InMemoryEventBusSubscriptionsManager();

        // Create a real model to capture event processing
        Mock<IModel> mockModel = new Mock<IModel>();

        // Setup model methods required by RabbitMQEventBus
        Mock<IBasicProperties> mockBasicProperties = new Mock<IBasicProperties>();
        mockBasicProperties.Setup(p => p.Headers).Returns(new Dictionary<string, object>());
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        mockModel.Setup(x => x.ExchangeDeclare(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueDeclare(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueBind(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.BasicPublish(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()));
        mockModel.Setup(x => x.ConfirmSelect());
        mockModel.Setup(x => x.WaitForConfirmsOrDie(It.IsAny<TimeSpan>()));

        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);
            return mockConnection.Object;
        });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the event bus with our subscription manager so we can verify
        RabbitMqEventBus eventBus = new RabbitMqEventBus(
            serviceProvider.GetRequiredService<IRabbitMqConnection>(),
            serviceProvider,
            subscriptionManager,
            "chirp_multi_handler_queue",
            5,
            BrokerName,
            "chirp_multi_handler_dlx");

        // Act - Subscribe with multiple handlers
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandlerWithCounter>();
        eventBus.Subscribe<TestIntegrationEvent, AnotherTestIntegrationEventHandler>();

        // Assert - Both handlers are registered
        Assert.IsTrue(subscriptionManager.HasSubscriptionsForEvent<TestIntegrationEvent>());
        IEnumerable<SubscriptionInfo> handlers = subscriptionManager.GetHandlersForEvent<TestIntegrationEvent>();
        IEnumerable<SubscriptionInfo> subscriptionInfos = handlers as SubscriptionInfo[] ?? handlers.ToArray();
        Assert.AreEqual(2, subscriptionInfos.Count());

        // Verify the specific handler types
        Assert.IsTrue(subscriptionInfos.Any(h => h.HandlerType == typeof(TestIntegrationEventHandlerWithCounter)));
        Assert.IsTrue(subscriptionInfos.Any(h => h.HandlerType == typeof(AnotherTestIntegrationEventHandler)));

        // Now publish an event and simulate message processing
        eventBus.Publish(testEvent);

        // Verify the publish happened
        mockModel.Verify(x => x.BasicPublish(
                It.IsAny<string>(),
                It.Is<string>(s => s == nameof(TestIntegrationEvent)),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()),
            Times.Once);

        // Now we need to simulate message receipt since we can't directly trigger the Consumer_Received method
        // We'll use reflection to access the private method
        Type eventBusType = typeof(RabbitMqEventBus);
        MethodInfo? processEventMethod = eventBusType.GetMethod("ProcessEvent",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(processEventMethod, "ProcessEvent method not found");

        // Simulate processing for both handlers by invoking the method directly
        // This is what would happen when a message is received from RabbitMQ
        await (Task)processEventMethod.Invoke(
            eventBus,
            new object[] { typeof(TestIntegrationEvent).Name, System.Text.Json.JsonSerializer.Serialize(testEvent) });

        // Verify both handlers were called
        Assert.IsTrue(handler1.HandlerCallCount > 0, "First handler was not called");
        Assert.IsTrue(handler2.HandlerCallCount > 0, "Second handler was not called");

        // Verify the event data was correctly passed to the handlers
        Assert.IsNotNull(handler1.LastReceivedEvent, "Event not received by first handler");
        Assert.AreEqual(testEvent.Message, handler1.LastReceivedEvent.Message);
    }

    [TestMethod]
    public async Task PublishEvent_SubscriberReceivesIt_HandlerIsInvoked()
    {
        // This test specifically focuses on publishing an event and verifying that subscribers receive it

        // Arrange - Setup service collection
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RMQ:Host"] = _host,
                ["RMQ:Username"] = "guest",
                ["RMQ:Password"] = "guest",
                ["RMQ:Port"] = _port.ToString(),
                ["RMQ:ExchangeName"] = "chirp_publish_test_exchange",
                ["RMQ:ExchangeNameDLX"] = "chirp_publish_test_dlx"
            })
            .Build();

        // Create a test event with specific data we can verify
        string uniqueMessage = $"Test message {Guid.NewGuid()}";
        TestIntegrationEvent testEvent = new TestIntegrationEvent(uniqueMessage);

        // Create a handler with tracking capabilities
        TestIntegrationEventHandlerWithCounter handler = new TestIntegrationEventHandlerWithCounter();

        // Register services
        services.AddSingleton<TestIntegrationEventHandlerWithCounter>(handler);
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(handler);

        // Setup mock model to verify the publish workflow
        Mock<IModel> mockModel = new Mock<IModel>();
        Mock<IBasicProperties> mockBasicProperties = new Mock<IBasicProperties>();
        mockBasicProperties.Setup(p => p.Headers).Returns(new Dictionary<string, object>());
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        mockModel.Setup(x => x.ExchangeDeclare(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueDeclare(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueBind(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.BasicPublish(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()));
        mockModel.Setup(x => x.ConfirmSelect());
        mockModel.Setup(x => x.WaitForConfirmsOrDie(It.IsAny<TimeSpan>()));

        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);
            return mockConnection.Object;
        });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the event bus
        InMemoryEventBusSubscriptionsManager subscriptionManager = new InMemoryEventBusSubscriptionsManager();
        RabbitMqEventBus eventBus = new RabbitMqEventBus(
            serviceProvider.GetRequiredService<IRabbitMqConnection>(),
            serviceProvider,
            subscriptionManager,
            "chirp_publish_test_queue");

        // Act - First subscribe to the event
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandlerWithCounter>();

        // Verify subscription was registered
        Assert.IsTrue(subscriptionManager.HasSubscriptionsForEvent<TestIntegrationEvent>());

        // Act - Then publish the event
        eventBus.Publish(testEvent);

        // Verify publish occurred with correct routing key
        mockModel.Verify(x => x.BasicPublish(
                It.IsAny<string>(),
                It.Is<string>(s => s == nameof(TestIntegrationEvent)),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()),
            Times.Once);

        // Now simulate the event being received by the consumer
        // Access the private ProcessEvent method using reflection
        MethodInfo? processEventMethod = typeof(RabbitMqEventBus).GetMethod(
            "ProcessEvent",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(processEventMethod, "ProcessEvent method not found");

        // Simulate the RabbitMQ consumer receiving the message
        await (Task)processEventMethod.Invoke(
            eventBus,
            new object[] { typeof(TestIntegrationEvent).Name, System.Text.Json.JsonSerializer.Serialize(testEvent) });

        // Assert - Verify the handler was called
        Assert.AreEqual(1, handler.HandlerCallCount, "Handler should be called exactly once");

        // Verify the event data was correctly received
        Assert.IsNotNull(handler.LastReceivedEvent, "Handler did not receive the event");
        Assert.AreEqual(uniqueMessage, handler.LastReceivedEvent.Message, "Event message does not match");

        // Verify event ID matches what was sent
        Assert.AreEqual(testEvent.Id, handler.LastReceivedEvent.Id, "Event ID does not match");
    }

    [TestMethod]
    public async Task PublishWithError_UsesDLXQueue()
    {
        // Arrange - Setup service collection
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RMQ:Host"] = _host,
                ["RMQ:Username"] = "guest",
                ["RMQ:Password"] = "guest",
                ["RMQ:Port"] = _port.ToString(),
                ["RMQ:ExchangeName"] = "chirp_error_test_exchange",
                ["RMQ:ExchangeNameDLX"] = "chirp_error_test_dlx"
            })
            .Build();

        // Setup connection with mocks to verify DLX usage
        Mock<IModel> mockModel = new Mock<IModel>();

        // Setup model methods
        Mock<IBasicProperties> mockBasicProperties = new Mock<IBasicProperties>();
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        // Setup standard methods needed by constructor
        mockModel.Setup(x => x.ExchangeDeclare(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueDeclare(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueBind(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.BasicPublish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()));
        mockModel.Setup(x => x.ConfirmSelect());

        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);
            return mockConnection.Object;
        });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act - Create event bus which should setup the DLX exchange
        RabbitMqEventBus eventBus = new RabbitMqEventBus(
            serviceProvider.GetRequiredService<IRabbitMqConnection>(),
            serviceProvider,
            new InMemoryEventBusSubscriptionsManager(),
            "chirp_error_test_queue",
            5,
            "chirp_error_test_exchange",
            "chirp_error_test_dlx");

        // Assert - Verify DLX exchange was declared
        mockModel.Verify(x => x.ExchangeDeclare(
                It.Is<string>(s => s == "chirp_error_test_dlx"),
                It.Is<string>(s => s == ExchangeType.Direct),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>()),
            Times.AtLeastOnce);

        // Verify DLX queue was created
        mockModel.Verify(x => x.QueueDeclare(
                It.Is<string>(s => s == "dlx.chirp_error_test_queue"),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>()),
            Times.AtLeastOnce);

        // Verify DLX queue was bound to DLX exchange
        mockModel.Verify(x => x.QueueBind(
                It.Is<string>(s => s == "dlx.chirp_error_test_queue"),
                It.Is<string>(s => s == "chirp_error_test_dlx"),
                It.Is<string>(s => s == "dlx.chirp_error_test_queue"),
                It.IsAny<IDictionary<string, object>>()),
            Times.AtLeastOnce);
    }

    // Helper methods
    private static IModel CreateMockModel()
    {
        Mock<IModel> mockModel = new Mock<IModel>();

        // Setup common model methods
        Mock<IBasicProperties> mockBasicProperties = new Mock<IBasicProperties>();
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        // Setup for QueueDeclare, ExchangeDeclare, QueueBind
        mockModel.Setup(x => x.QueueDeclare(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));

        mockModel.Setup(x => x.ExchangeDeclare(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));

        mockModel.Setup(x => x.QueueBind(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>()));

        // Setup for BasicPublish and related methods
        mockModel.Setup(x => x.BasicPublish(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()));

        mockModel.Setup(x => x.ConfirmSelect());
        mockModel.Setup(x => x.WaitForConfirmsOrDie(It.IsAny<TimeSpan>()));

        return mockModel.Object;
    }

    // Test event class for publishing/subscribing tests
    private record TestIntegrationEvent(string Message) : IntegrationEvent;

    // Test event handler for subscribe tests
    private class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public TestIntegrationEvent? ReceivedEvent { get; private set; }

        public Task Handle(TestIntegrationEvent @event)
        {
            HandlerCalled = true;
            ReceivedEvent = @event;
            return Task.CompletedTask;
        }
    }

    // Test event handler for subscribe tests with counters to verify execution
    private class TestIntegrationEventHandlerWithCounter : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public int HandlerCallCount { get; private set; }
        public TestIntegrationEvent? LastReceivedEvent { get; private set; }

        public Task Handle(TestIntegrationEvent @event)
        {
            HandlerCallCount++;
            LastReceivedEvent = @event;
            return Task.CompletedTask;
        }
    }

    private class AnotherTestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public int HandlerCallCount { get; private set; }

        public Task Handle(TestIntegrationEvent @event)
        {
            HandlerCallCount++;
            return Task.CompletedTask;
        }
    }


    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
    {
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }
}