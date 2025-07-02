using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public sealed class RabbitMQEventBusTests
{
    [TestMethod]
    public void Constructor_InitializesProperties_Successfully()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

        // Act
        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Assert
        Assert.IsNotNull(eventBus);
        mockRabbitConnection.Verify(x => x.CreateModel(), Times.AtLeastOnce);
    }

    [TestMethod]
    public void Constructor_WithNullRabbitMQConnection_ThrowsArgumentNullException()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RabbitMqEventBus(
            null!,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue"));
    }

    [TestMethod]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RabbitMqEventBus(
            mockRabbitConnection.Object,
            null!,
            subscriptionsManager,
            "test_queue"));
    }

    [TestMethod]
    public void Constructor_WithNullSubscriptionsManager_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            null!,
            "test_queue"));
    }

    [TestMethod]
    public void Constructor_WithNullExchangeName_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue",
            5,
            null!));
    }

    [TestMethod]
    public void Constructor_WithNullDlxExchangeName_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(CreateMockModel());

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue",
            5,
            "exchange",
            null!));
    }

    [TestMethod]
    public void Publish_CallsRabbitMQPublishMethod()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = CreateMockModel();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel);

        var testEvent = new TestIntegrationEvent("Test message");
        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Publish(testEvent);

        // Assert
        mockRabbitConnection.Verify(x => x.CreateModel(), Times.AtLeast(2)); // Once for constructor, once for publish
        Assert.IsNotNull(eventBus);
    }

    [TestMethod]
    public void Publish_CreatesExchangeAndBasicProperties()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = new Mock<IModel>();
        var mockBasicProperties = new Mock<IBasicProperties>();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        var testEvent = new TestIntegrationEvent("Test message");
        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Publish(testEvent);

        // Assert
        mockModel.Verify(x => x.ExchangeDeclare(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>()),
            Times.AtLeastOnce);

        mockModel.Verify(x => x.CreateBasicProperties(), Times.Once);
        mockModel.Verify(x => x.BasicPublish(
                It.IsAny<string>(),
                It.Is<string>(s => s == testEvent.GetType().Name),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()),
            Times.Once);
    }

    [TestMethod]
    public void Publish_WhenConnectionNotEstablished_TriesToConnect()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = CreateMockModel();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(false);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel);

        var testEvent = new TestIntegrationEvent("Test message");
        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Publish(testEvent);

        // Assert
        mockRabbitConnection.Verify(x => x.TryConnect(), Times.AtLeastOnce);
    }

    [TestMethod]
    public void Subscribe_RegistersEventHandler()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = CreateMockModel();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel);

        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<TestIntegrationEvent>());
    }

    [TestMethod]
    public void Subscribe_BindsQueueToExchange()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = new Mock<IModel>();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);

        // Required model setups for constructor
        mockModel.Setup(x => x.ExchangeDeclare(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>()));
        mockModel.Setup(x => x.QueueDeclare(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        var mockBasicProperties = new Mock<IBasicProperties>();
        mockModel.Setup(x => x.CreateBasicProperties()).Returns(mockBasicProperties.Object);

        const string queueName = "test_queue";
        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            queueName);

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        mockModel.Verify(x => x.QueueBind(
                It.Is<string>(q => q == queueName),
                It.IsAny<string>(),
                It.Is<string>(r => r == typeof(TestIntegrationEvent).Name),
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);
    }

    [TestMethod]
    public void Subscribe_WhenConnectionNotEstablished_TriesToConnect()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = CreateMockModel();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(false);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel);

        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Assert
        mockRabbitConnection.Verify(x => x.TryConnect(), Times.AtLeastOnce);
    }

    [TestMethod]
    public void Subscribe_RegistersMultipleHandlersForSameEvent()
    {
        // Arrange
        var mockRabbitConnection = new Mock<IRabbitMqConnection>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        var mockModel = CreateMockModel();

        mockRabbitConnection.Setup(x => x.IsConnected).Returns(true);
        mockRabbitConnection.Setup(x => x.CreateModel()).Returns(mockModel);

        var eventBus = new RabbitMqEventBus(
            mockRabbitConnection.Object,
            mockServiceProvider.Object,
            subscriptionsManager,
            "test_queue");

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();
        eventBus.Subscribe<TestIntegrationEvent, AnotherTestIntegrationEventHandler>();

        // Assert
        Assert.IsTrue(subscriptionsManager.HasSubscriptionsForEvent<TestIntegrationEvent>());
        var handlers = subscriptionsManager.GetHandlersForEvent<TestIntegrationEvent>();
        Assert.AreEqual(2, handlers.Count());
    }

    // Helper methods
    private static IModel CreateMockModel()
    {
        var mockModel = new Mock<IModel>();

        // Setup common model methods
        var mockBasicProperties = new Mock<IBasicProperties>();
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

    // Test event class for testing
    private record TestIntegrationEvent(string Message) : IntegrationEvent;

    // Test event handlers for testing
    private class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task Handle(TestIntegrationEvent @event)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherTestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task Handle(TestIntegrationEvent @event)
        {
            return Task.CompletedTask;
        }
    }
}