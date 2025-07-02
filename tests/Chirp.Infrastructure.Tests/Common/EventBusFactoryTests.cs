using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace Chirp.Infrastructure.Tests.Common;

[TestClass]
public class EventBusFactoryTests
{
    [TestMethod]
    public void Create_RabbitMQEventBusType_ReturnsRabbitMQEventBus()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IRabbitMqConnection> mockConnection = new();
        Mock<IConfiguration> mockConfiguration = new();

        // Set up configuration values
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns("test_exchange");
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns("test_dlx_exchange");

        // Set up service provider to return mock connection
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IRabbitMqConnection)))
            .Returns(mockConnection.Object);

        // Act
        IEventBus result = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue",
            5);

        // Assert
        Assert.IsInstanceOfType<RabbitMqEventBus>(result);
    }

    [TestMethod]
    public void Create_RabbitMQEventBusType_WithDefaultExchangeNames_ReturnsRabbitMQEventBus()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Set up configuration to return null for exchange names (use defaults)
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns((string)null);
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns((string)null);

        // Set up service provider to return mock connection
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IRabbitMqConnection)))
            .Returns(mockConnection.Object);

        // Act
        IEventBus result = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue",
            5);

        // Assert
        Assert.IsInstanceOfType<RabbitMqEventBus>(result);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Create_RabbitMQEventBusType_ConnectionNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Set up service provider to return null for connection (not registered)
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IRabbitMqConnection)))
            .Returns(null);

        // Act - should throw InvalidOperationException
        EventBusFactory.Create(
            EventBusType.RabbitMQ,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_KafkaEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.Kafka,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_topic");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_AzureServiceBusEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.AzureServiceBus,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_AmazonSqsEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.AmazonSqs,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_RedisEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.Redis,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_channel");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_GooglePubSubEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.GooglePubSub,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_topic");
    }

    [TestMethod]
    [ExpectedException(typeof(NotImplementedException))]
    public void Create_NATSEventBusType_ThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw NotImplementedException
        EventBusFactory.Create(
            EventBusType.NATS,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_subject");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Create_InvalidEventBusType_ThrowsArgumentException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Act - should throw ArgumentException
        EventBusFactory.Create(
            (EventBusType)999, // Invalid enum value
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue");
    }

    [TestMethod]
    public void Create_VerifyInMemoryEventBusSubscriptionsManager()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new Mock<IServiceProvider>();
        Mock<IRabbitMqConnection> mockConnection = new Mock<IRabbitMqConnection>();
        Mock<IConfiguration> mockConfiguration = new Mock<IConfiguration>();

        // Set up service provider to return mock connection
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IRabbitMqConnection)))
            .Returns(mockConnection.Object);

        // Act
        IEventBus result = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            mockServiceProvider.Object,
            mockConfiguration.Object,
            "test_queue");

        // Assert - we can't directly verify the subscription manager as it's private
        // but we can verify the result is created and the mock was called
        mockServiceProvider.Verify(sp => sp.GetService(typeof(IRabbitMqConnection)), Times.Once);
    }
}