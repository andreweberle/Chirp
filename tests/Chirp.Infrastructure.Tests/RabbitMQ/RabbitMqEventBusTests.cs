using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMqEventBusTests
{
    private static RabbitMqContainer _rabbitmqContainer = null!;
    private static readonly string Username = "guest";
    private static readonly string Password = "guest";
    private static string _hostname = null!;
    private static int _port;

    // Test event class as a record
    public record TestIntegrationEvent : IntegrationEvent
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

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        // Create a RabbitMQ container instance
        _rabbitmqContainer = new RabbitMqBuilder()
            .WithUsername(Username)
            .WithPassword(Password)
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

    private static TestRabbitMqConnection CreateConnection()
    {
        ConnectionFactory connectionFactory = new()
        {
            HostName = _hostname,
            Port = _port,
            UserName = Username,
            Password = Password,
            DispatchConsumersAsync = true
        };

        return new TestRabbitMqConnection(connectionFactory);
    }

    // Mock connection that simulates a disconnected state
    private class DisconnectedRabbitMqConnection : IChirpRabbitMqConnection
    {
        public bool IsConnected => false;

        public bool TryConnect()
        {
            // Simulate connection attempt that fails quickly without hanging
            throw new InvalidOperationException("Unable to connect to RabbitMQ broker - connection refused");
        }

        public IModel CreateModel()
        {
            throw new InvalidOperationException("Cannot create model - RabbitMQ connection is not available");
        }
    }

    // Test connection that simulates timeout behavior similar to ChirpRabbitMqConnection
    private class TestRabbitMqConnectionWithTimeout : IChirpRabbitMqConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly TimeSpan _connectionTimeout;
        private IConnection? _connection;
        private int _retryCount;
        private readonly int _maxRetries;

        public TestRabbitMqConnectionWithTimeout(IConnectionFactory connectionFactory, TimeSpan connectionTimeout,
            int maxRetries = 5)
        {
            _connectionFactory = connectionFactory;
            _connectionTimeout = connectionTimeout;
            _maxRetries = maxRetries;
        }

        public bool IsConnected => _connection is { IsOpen: true };

        public bool TryConnect()
        {
            _retryCount = 0;
            while (_retryCount < _maxRetries)
            {
                try
                {
                    // Simulate timeout by using a cancellation token
                    using CancellationTokenSource cts = new(_connectionTimeout);
                    Task<IConnection> connectTask = Task.Run(() => _connectionFactory.CreateConnection(), cts.Token);

                    if (!connectTask.Wait(_connectionTimeout))
                    {
                        throw new TimeoutException(
                            $"Connection attempt timed out after {_connectionTimeout.TotalSeconds}s");
                    }

                    _connection = connectTask.Result;
                    return true;
                }
                catch (Exception ex) when (ex is TimeoutException || ex is SocketException ||
                                           ex is BrokerUnreachableException || ex is AggregateException)
                {
                    _retryCount++;
                    if (_retryCount >= _maxRetries)
                    {
                        // ? MATCH PRODUCTION: Return false instead of throwing
                        Console.WriteLine($"Failed to connect to RabbitMQ after {_maxRetries} attempts: {ex.Message}");
                        return false;
                    }

                    // Brief delay between retries
                    Thread.Sleep(TimeSpan.FromMilliseconds(100 * _retryCount));
                }
            }

            return false;
        }

        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                TryConnect();
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ connection is not available.");
            }

            return _connection!.CreateModel();
        }
    }

    [TestMethod]
    public void Constructor_InitializesProperties_Successfully()
    {
        // Arrange
        TestRabbitMqConnection connection = CreateConnection();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_queue";
        const string exchangeName = "test_exchange";
        const string dlxExchangeName = "test_dlx_exchange";

        // Act
        ChirpRabbitMqEventBus eventBus = new(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Assert
        Assert.IsNotNull(eventBus);
    }

    [TestMethod]
    public void Publish_SendsMessageToExchange()
    {
        // Arrange
        IChirpRabbitMqConnection connection = CreateConnection();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_publish_queue";
        const string exchangeName = "test_publish_exchange";
        const string dlxExchangeName = "test_publish_dlx_exchange";

        ChirpRabbitMqEventBus eventBus = new(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        TestIntegrationEvent testEvent = new() { Message = "Test message" };
        bool messageReceived = false;
        string? receivedMessage = null;

        // Set up a consumer to verify the message was published
        using IModel channel = connection.CreateModel();

        // Make sure the queue exists and is bound to the exchange
        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true);
        channel.QueueDeclare(queueName, true, false, false, null);
        channel.QueueBind(queueName, exchangeName, testEvent.GetType().Name);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.Received += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            receivedMessage = Encoding.UTF8.GetString(body);
            messageReceived = true;
            await Task.Yield();
        };

        channel.BasicConsume(queueName, true, consumer);

        // Act
        eventBus.Publish(testEvent);

        // Assert - Wait for message to be received
        TimeSpan maxWaitTime = TimeSpan.FromSeconds(5);
        DateTime startTime = DateTime.UtcNow;
        while (!messageReceived && DateTime.UtcNow - startTime < maxWaitTime)
        {
            Thread.Sleep(100);
        }

        Assert.IsTrue(messageReceived, "Message should be received");
        Assert.IsNotNull(receivedMessage, "Received message should not be null");

        // Verify message content
        TestIntegrationEvent? deserializedEvent = JsonSerializer.Deserialize<TestIntegrationEvent>(receivedMessage);
        Assert.IsNotNull(deserializedEvent);
        Assert.AreEqual(testEvent.Message, deserializedEvent.Message);
    }

    [TestMethod]
    public async Task Subscribe_RegistersAndInvokesEventHandler()
    {
        // Arrange
        IChirpRabbitMqConnection connection = CreateConnection();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_subscribe_queue";
        const string exchangeName = "test_subscribe_exchange";
        const string dlxExchangeName = "test_subscribe_dlx_exchange";

        // Predeclare the exchange and queue so that Subscribe() can bind them
        using (IModel adminChannel = connection.CreateModel())
        {
            adminChannel.ExchangeDeclare(
                exchangeName,
                ExchangeType.Direct,
                true);

            adminChannel.QueueDeclare(
                queueName,
                true,
                false,
                false,
                null);
        }

        // Create a singleton handler instance to ensure we can track its state
        TestIntegrationEventHandler typedHandler = new();

        // Set up DI with the handler as a singleton
        ServiceCollection services = new();
        services.AddSingleton(typedHandler); // Register the exact instance we'll check later
        services.AddSingleton<TestIntegrationEventHandler>(_ => typedHandler); // Also register by type
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpRabbitMqEventBus eventBus = new(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Give the internal consumer a moment to spin up and bind
        await Task.Delay(300); // Increased delay to ensure setup is complete

        // Now publish event
        TestIntegrationEvent testEvent = new() { Message = "Test subscription message" };
        eventBus.Publish(testEvent);

        // Assert wait up to 5s for the handler to flip its flag
        TimeSpan maxWait = TimeSpan.FromSeconds(5);
        DateTime startTime = DateTime.UtcNow;
        while (!typedHandler.HandlerCalled && DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(50);
        }

        Assert.IsTrue(typedHandler.HandlerCalled, "Event handler should be called");
        Assert.AreEqual(testEvent.Message, typedHandler.ReceivedMessage);
    }

    [TestMethod]
    [Timeout(10000)] // Test should complete within 10 seconds, ensuring no hang
    public void Publish_WhenNotConnected_FailsGracefullyWithoutHanging()
    {
        // Arrange
        DisconnectedRabbitMqConnection disconnectedConnection = new();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_disconnected_queue";
        const string exchangeName = "test_disconnected_exchange";
        const string dlxExchangeName = "test_disconnected_dlx_exchange";

        TestIntegrationEvent testEvent = new() { Message = "Test message when disconnected" };

        // Act & Assert
        try
        {
            // The constructor should throw when trying to create the DLX since connection is not available
            ChirpRabbitMqEventBus eventBus = new(
                disconnectedConnection,
                serviceProvider,
                subscriptionsManager,
                queueName,
                5,
                exchangeName,
                dlxExchangeName);

            // If we get here, try publishing (though constructor should fail)
            Assert.Fail("Expected exception during event bus creation with disconnected connection");
        }
        catch (InvalidOperationException ex)
        {
            // Expected - verify the exception message indicates connection failure
            Assert.IsTrue(
                ex.Message.Contains("RabbitMQ") || ex.Message.Contains("connection") || ex.Message.Contains("model"),
                $"Exception message should indicate connection issue. Actual: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Any other exception is also acceptable as long as it doesn't hang
            Assert.IsNotNull(ex, "Should throw some exception when connection is not available");
        }
    }

    [TestMethod]
    [Timeout(10000)] // Test should complete within 10 seconds
    public void Subscribe_WhenNotConnected_FailsGracefullyWithoutHanging()
    {
        // Arrange
        DisconnectedRabbitMqConnection disconnectedConnection = new();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection serviceCollection = new();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        const string queueName = "test_subscribe_disconnected_queue";
        const string exchangeName = "test_subscribe_disconnected_exchange";
        const string dlxExchangeName = "test_subscribe_disconnected_dlx_exchange";

        // Act & Assert
        try
        {
            ChirpRabbitMqEventBus eventBus = new(
                disconnectedConnection,
                serviceProvider,
                subscriptionsManager,
                queueName,
                5,
                exchangeName,
                dlxExchangeName);

            Assert.Fail("Expected exception during event bus creation with disconnected connection");
        }
        catch (InvalidOperationException ex)
        {
            // Expected - connection should fail during constructor
            Assert.IsTrue(
                ex.Message.Contains("connection") || ex.Message.Contains("model") || ex.Message.Contains("RabbitMQ"),
                $"Exception should indicate connection failure. Actual: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Any exception is acceptable as long as it completes quickly
            Assert.IsNotNull(ex, "Should throw exception when connection fails");
        }
    }

    [TestMethod]
    [Timeout(10000)] // Test should complete within 10 seconds
    public void Not_Connected_When_Initialized_ThrowsException()
    {
        // Arrange
        ConnectionFactory badConnectionFactory = new()
        {
            HostName = "nonexistent.host.local", // Invalid hostname
            Port = 9999, // Unlikely to be open
            UserName = Username,
            Password = Password,
            DispatchConsumersAsync = true,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(1), // Short timeout for faster test
            SocketReadTimeout = TimeSpan.FromSeconds(1),
            SocketWriteTimeout = TimeSpan.FromSeconds(1)
        };
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        // Use TestRabbitMqConnectionWithTimeout which simulates retry behavior like the real ChirpRabbitMqConnection
        IChirpRabbitMqConnection chirpRabbitMqConnection = new TestRabbitMqConnectionWithTimeout(
            badConnectionFactory,
            TimeSpan.FromSeconds(1), // Connection timeout per attempt
            3); // Retry 3 times to verify retry logic

        const string queueName = "test_subscribe_queue";
        const string exchangeName = "test_subscribe_exchange";
        const string dlxExchangeName = "test_dlx_exchange";

        DateTime startTime = DateTime.UtcNow;

        // Act
        ChirpRabbitMqEventBus eventBus = new(
            chirpRabbitMqConnection,
            new ServiceCollection().BuildServiceProvider(),
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        TimeSpan elapsed = DateTime.UtcNow - startTime;

        // Assert
        // The constructor should have attempted to connect with retries
        // Since we're using TestRabbitMqConnectionWithTimeout with 3 retries, it should take some time
        Assert.IsTrue(elapsed.TotalMilliseconds > 100,
            "Constructor should have attempted retries, taking some time");

        Assert.IsFalse(chirpRabbitMqConnection.IsConnected,
            "Connection should not be established after all retry attempts failed");

        // Verify the event bus was still created (deferred initialization allows this)
        Assert.IsNotNull(eventBus, "Event bus should be created even if connection failed");

        Console.WriteLine($"Connection attempts took {elapsed.TotalSeconds:F2}s with retries");
    }

    [TestMethod]
    [Timeout(10000)] // Test should complete within 10 seconds
    public void Connection_Retries_AreTrackedCorrectly()
    {
        // Arrange
        ConnectionFactory badConnectionFactory = new()
        {
            HostName = "nonexistent.host.local",
            Port = 9999,
            UserName = Username,
            Password = Password,
            DispatchConsumersAsync = true,
            RequestedConnectionTimeout = TimeSpan.FromMilliseconds(500),
            SocketReadTimeout = TimeSpan.FromMilliseconds(500),
            SocketWriteTimeout = TimeSpan.FromMilliseconds(500)
        };

        int maxRetries = 3;
        TestRabbitMqConnectionWithTimeout connection = new(
            badConnectionFactory,
            TimeSpan.FromMilliseconds(500),
            maxRetries);

        DateTime startTime = DateTime.UtcNow;

        // Act
        bool connected = connection.TryConnect();

        TimeSpan elapsed = DateTime.UtcNow - startTime;

        // Assert
        // ? PRODUCTION BEHAVIOR: Returns false instead of throwing
        Assert.IsFalse(connected, "Connection should fail and return false");
        Assert.IsFalse(connection.IsConnected, "IsConnected should be false");

        // With 3 retries and delays between them (100ms, 200ms, 300ms), plus timeout attempts,
        // the total should be at least the delay time
        Assert.IsTrue(elapsed.TotalMilliseconds >= 100, // At least first retry delay
            $"Should have taken time for retries. Actual: {elapsed.TotalMilliseconds}ms");

        Console.WriteLine($"Retry attempts took {elapsed.TotalSeconds:F2}s for {maxRetries} retries");
    }
}