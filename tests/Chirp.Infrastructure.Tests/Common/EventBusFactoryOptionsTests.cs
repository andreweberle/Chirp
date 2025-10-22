using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.Common;

[TestClass]
public class EventBusFactoryOptionsTests
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
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();

        // Start the container
        await _rabbitmqContainer.StartAsync();

        // Get connection details
        _hostname = _rabbitmqContainer.Hostname;
        _port = _rabbitmqContainer.GetMappedPublicPort(5672);
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
    {
        if (_rabbitmqContainer != null)
        {
            await _rabbitmqContainer.StopAsync();
            await _rabbitmqContainer.DisposeAsync();
        }
    }

    [TestMethod]
    public void Create_WithRabbitMqChirpOptions_ReturnsRabbitMQEventBus()
    {
        // Arrange
        RabbitMqChirpOptions options = new()
        {
            QueueName = "test_queue",
            RetryCount = 5,
            ExchangeName = "test_exchange",
            DeadLetterExchangeName = "test_dlx_exchange"
        };

        // Arrange
        ServiceCollection serviceCollection = new();

        // Create a connection factory using the test container
        ConnectionFactory connectionFactory = new()
        {
            HostName = _hostname,
            Port = _port,
            UserName = _username,
            Password = _password,
            DispatchConsumersAsync = true
        };

        // Create the connection
        TestRabbitMqConnection connection = new(connectionFactory);

        // Create mock configuration
        Mock<IConfiguration> mockConfiguration = new();

        // Register the connection with the service provider
        serviceCollection.AddSingleton<IChirpRabbitMqConnection>(connection);
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        // Create mock configuration
        mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns("test_exchange");
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns("test_dlx_exchange");

        // Act
        IChirpEventBus result = EventBusFactory.Create(
            options,
            serviceProvider,
            mockConfiguration.Object);

        // Assert
        Assert.IsInstanceOfType<ChirpRabbitMqEventBus>(result);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Create_WithRabbitMqChirpOptions_ConnectionNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new RabbitMqChirpOptions
        {
            QueueName = "test_queue",
            RetryCount = 5
        };

        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        // Setup to return null for the connection, simulating not registered
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IChirpRabbitMqConnection)))
            .Returns(null);

        // Act - should throw InvalidOperationException
        EventBusFactory.Create(
            options,
            mockServiceProvider.Object,
            mockConfiguration.Object);
    }


    [TestMethod]
    public void Create_WithBaseChirpOptions_UsesEventBusTypeProperty()
    {
        // Arrange
        ChirpOptions options = new()
        {
            EventBusType = EventBusType.RabbitMQ,
            QueueName = "test_queue",
            RetryCount = 5
        };

        // Arrange
        ServiceCollection serviceCollection = new();

        // Create a connection factory using the test container
        ConnectionFactory connectionFactory = new()
        {
            HostName = _hostname,
            Port = _port,
            UserName = _username,
            Password = _password,
            DispatchConsumersAsync = true
        };

        // Create the connection
        TestRabbitMqConnection connection = new(connectionFactory);

        // Create mock configuration
        Mock<IConfiguration> mockConfiguration = new();

        // Register the connection with the service provider
        serviceCollection.AddSingleton<IChirpRabbitMqConnection>(connection);
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        // Create mock configuration
        mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["RMQ:ExchangeName"]).Returns("test_exchange");
        mockConfiguration.Setup(c => c["RMQ:ExchangeNameDLX"]).Returns("test_dlx_exchange");

        // Act
        IChirpEventBus result = EventBusFactory.Create(
            options,
            serviceProvider,
            mockConfiguration.Object);

        // Assert
        Assert.IsInstanceOfType<ChirpRabbitMqEventBus>(result);

        // Verify the connection was requested
        IChirpRabbitMqConnection? connectionService = serviceProvider.GetService<IChirpRabbitMqConnection>();
        Assert.IsNotNull(connectionService, "IChirpRabbitMqConnection should be registered in the service provider.");
        Assert.IsInstanceOfType<TestRabbitMqConnection>(connectionService,
            "Connection should be of type TestRabbitMqConnection.");
        Assert.IsTrue(connectionService.IsConnected, "Connection should be established successfully.");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Create_WithBaseChirpOptions_InvalidEventBusType_ThrowsArgumentException()
    {
        // Arrange
        var options = new ChirpOptions
        {
            EventBusType = (EventBusType)999, // Invalid enum value
            QueueName = "test_queue",
            RetryCount = 5
        };

        Mock<IServiceProvider> mockServiceProvider = new();
        Mock<IConfiguration> mockConfiguration = new();

        // Act - should throw ArgumentException
        EventBusFactory.Create(
            options,
            mockServiceProvider.Object,
            mockConfiguration.Object);
    }

    // Test-specific implementation of IChirpRabbitMqConnection
    private class TestRabbitMqConnection : IChirpRabbitMqConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection? _connection;

        public TestRabbitMqConnection(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            TryConnect();
        }

        public bool IsConnected => _connection is { IsOpen: true };

        public bool TryConnect()
        {
            try
            {
                _connection = _connectionFactory.CreateConnection();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IModel CreateModel()
        {
            if (!IsConnected) TryConnect();
            return _connection!.CreateModel();
        }
    }
}