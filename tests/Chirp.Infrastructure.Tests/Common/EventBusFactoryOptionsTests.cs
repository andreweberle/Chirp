using Chirp.Application.Common;
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

        try
        {
            // Act - should throw InvalidOperationException
            EventBusFactory.Create(
                options,
                mockServiceProvider.Object,
                mockConfiguration.Object);
        }
        catch (InvalidOperationException)
        {
        }
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

        // Add Chirp Logging.
        serviceCollection.AddSingleton(options);
        serviceCollection.AddSingleton<ChirpLogger>();

        // Create a connection factory using the test container
        ConnectionFactory connectionFactory = new()
        {
            HostName = _hostname,
            Port = _port,
            UserName = _username,
            Password = _password,
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

        try
        {
            // Act - should throw ArgumentException
            EventBusFactory.Create(
                options,
                mockServiceProvider.Object,
                mockConfiguration.Object);
        }
        catch (ArgumentException)
        {
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
            TryConnectAsync().GetAwaiter().GetResult();
        }

        public bool IsConnected => _connection is { IsOpen: true };


        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync();
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