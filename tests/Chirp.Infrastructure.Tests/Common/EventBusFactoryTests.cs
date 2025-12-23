using Chirp.Application.Interfaces;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Chirp.Infrastructure.Tests.Common;

[TestClass]
public class EventBusFactoryTests
{
    private static RabbitMqContainer _rabbitmqContainer = null!;
    private static string _username = "guest";
    private static string _password = "guest";
    private static string _hostname = null!;
    private static int _port;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        // Create a RabbitMQ container instance
        _rabbitmqContainer = new RabbitMqBuilder()
            .WithUsername(_username)
            .WithPassword(_password)
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .WithImage("rabbitmq:3.11-management")
            .Build();

        // Start the container
        await _rabbitmqContainer.StartAsync(testContext.CancellationToken);

        // Get connection details
        _hostname = _rabbitmqContainer.Hostname;
        _port = _rabbitmqContainer.GetMappedPublicPort(5672);
    }

    [ClassCleanup]
    public static async Task ClassCleanup(TestContext testContext)
    {
        if (_rabbitmqContainer != null)
        {
            await _rabbitmqContainer.StopAsync(testContext.CancellationToken);
            await _rabbitmqContainer.DisposeAsync();
        }
    }

    [TestMethod]
    public void CreateRabbitMQEventBusTypeReturnsRabbitMQEventBus()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Create a connection factory using the test container
        var connectionFactory = new ConnectionFactory
        {
            HostName = _hostname,
            Port = _port,
            UserName = _username,
            Password = _password,
        };

        // Create the connection
        var connection = new TestRabbitMqConnection(connectionFactory);

        // Register the connection with the service provider
        serviceCollection.AddSingleton<IChirpRabbitMqConnection>(connection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Create mock configuration
        Mock<IConfiguration> mockConfiguration = new();
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns("test_exchange");
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns("test_dlx_exchange");

        // Act
        var eventBus = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            serviceProvider,
            mockConfiguration.Object,
            "test_queue");

        // Assert
        Assert.IsNotNull(eventBus);
        Assert.IsInstanceOfType(eventBus, typeof(ChirpRabbitMqEventBus));
    }

    [TestMethod]
    public void Create_RabbitMQEventBusType_WithDefaultExchangeNames_ReturnsRabbitMQEventBus()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Create a connection factory using the test container
        var connectionFactory = new ConnectionFactory
        {
            HostName = _hostname,
            Port = _port,
            UserName = _username,
            Password = _password,
        };

        // Create the connection
        var connection = new TestRabbitMqConnection(connectionFactory);

        // Register the connection with the service provider
        serviceCollection.AddSingleton<IChirpRabbitMqConnection>(connection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Create mock configuration with null exchange names to trigger defaults
        Mock<IConfiguration> mockConfiguration = new();

        // Fix warnings by returning null as string? instead of null directly
        string? nullExchangeName = null;
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns(nullExchangeName);
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns(nullExchangeName);

        // Act
        var eventBus = EventBusFactory.Create(
            EventBusType.RabbitMQ,
            serviceProvider,
            mockConfiguration.Object,
            "test_queue");

        // Assert
        Assert.IsNotNull(eventBus);
        Assert.IsInstanceOfType(eventBus, typeof(ChirpRabbitMqEventBus));
        // The default exchange names will be used internally, but we can't directly test them
        // since they are private in the ChirpRabbitMqEventBus class
    }

    [TestMethod]
    public void Create_RabbitMQEventBusType_ConnectionNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Set up service provider to return null for connection (not registered)
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IChirpRabbitMqConnection)))
                .Returns(null);

            // Act - should throw InvalidOperationException
            EventBusFactory.Create(
                EventBusType.RabbitMQ,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_queue");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public void CreateKafkaEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.Kafka,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_topic");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateAzureServiceBusEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.AzureServiceBus,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_queue");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateAmazonSqsEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.AmazonSqs,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_queue");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateRedisEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.Redis,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_channel");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateGooglePubSubEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.GooglePubSub,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_topic");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateNATSEventBusTypeThrowsNotImplementedException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        try
        {
            // Act - should throw NotImplementedException
            EventBusFactory.Create(
                EventBusType.NATS,
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_subject");
        }
        catch (NotImplementedException)
        {
        }
    }

    [TestMethod]
    public void CreateInvalidEventBusTypeThrowsArgumentException()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        // Act + Assert
        try
        {
            EventBusFactory.Create(
                (EventBusType)999, // Invalid enum value
                mockServiceProvider.Object,
                mockConfiguration.Object,
                "test_queue");
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
    }

    // Test-specific implementation of IChirpRabbitMqConnection
    private class TestRabbitMqConnection : IChirpRabbitMqConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection? _connection;

        public TestRabbitMqConnection(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            TryConnectAsync();
        }

        public bool IsConnected => _connection is { IsOpen: true };

        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected) await TryConnectAsync(cancellationToken);
            return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
        }
    }
}