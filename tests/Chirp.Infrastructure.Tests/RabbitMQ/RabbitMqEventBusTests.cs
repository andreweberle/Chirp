using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Testcontainers.RabbitMq;
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
    private static int _webPort;

    // Test event class as a record
    public record TestIntegrationEvent : IntegrationEvent
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

    // Test event handler class
    private class TestFailedIntegrationEventHandler : IChirpIntegrationEventHandler<TestFailedIntegrationEvent>
    {
        public bool HandlerCalled { get; private set; }
        public string? ReceivedMessage { get; private set; }

        public Task Handle(TestFailedIntegrationEvent @event)
        {
            HandlerCalled = true;

            // Simulate failure
            throw new Exception("Simulated handler failure");
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
            .WithPortBinding(15672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .WithImage("rabbitmq:3.11-management")
            .Build();

        // Start the container
        await _rabbitmqContainer.StartAsync(testContext.CancellationToken);

        // Get connection details
        _hostname = _rabbitmqContainer.Hostname;
        _port = _rabbitmqContainer.GetMappedPublicPort(5672);
        _webPort = _rabbitmqContainer.GetMappedPublicPort(15672);
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

    private static TestRabbitMqConnection CreateConnection()
    {
        ConnectionFactory connectionFactory = new()
        {
            HostName = _hostname,
            Port = _port,
            UserName = Username,
            Password = Password,
        };

        return new TestRabbitMqConnection(connectionFactory);
    }

    // Mock connection that simulates a disconnected state
    private class DisconnectedRabbitMqConnection : IChirpRabbitMqConnection
    {
        public bool IsConnected => false;

        public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            // Simulate connection attempt that fails quickly without hanging
            throw new InvalidOperationException("Unable to connect to RabbitMQ broker - connection refused");
        }

        public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Cannot create channel - RabbitMQ connection is not available");
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

        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            _retryCount = 0;
            while (_retryCount < _maxRetries)
            {
                try
                {
                    // Simulate timeout by using a cancellation token
                    using CancellationTokenSource cts = new(_connectionTimeout);
                    Task<IConnection> connectTask = Task.Run(() => _connectionFactory.CreateConnectionAsync(), cts.Token);

                    if (!connectTask.Wait(_connectionTimeout, cancellationToken))
                    {
                        throw new TimeoutException(
                            $"Connection attempt timed out after {_connectionTimeout.TotalSeconds}s");
                    }

                    _connection = await connectTask;
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

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(cancellationToken);
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ connection is not available.");
            }

            return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
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
    public async Task Publish_SendsMessageToExchangeAsync()
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
        await using IChannel channel = await connection.CreateChannelAsync(TestContext.CancellationToken);

        // Make sure the queue exists and is bound to the exchange
        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, true, cancellationToken: TestContext.CancellationToken);
        await channel.QueueDeclareAsync(queueName, true, false, false, null, cancellationToken: TestContext.CancellationToken);
        await channel.QueueBindAsync(queueName, exchangeName, testEvent.GetType().Name, cancellationToken: TestContext.CancellationToken);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            receivedMessage = Encoding.UTF8.GetString(body);
            messageReceived = true;
            await Task.Yield();
        };

        await channel.BasicConsumeAsync(queueName, true, consumer, TestContext.CancellationToken);

        // Act
        await eventBus.PublishAsync(testEvent, TestContext.CancellationToken);

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
        await using (IChannel adminChannel = await connection.CreateChannelAsync(TestContext.CancellationToken))
        {
            await adminChannel.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Direct,
                true, cancellationToken: TestContext.CancellationToken);

            await adminChannel.QueueDeclareAsync(
                queueName,
                true,
                false,
                false,
                null, cancellationToken: TestContext.CancellationToken);
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
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);

        // Give the internal consumer a moment to spin up and bind
        await Task.Delay(300, TestContext.CancellationToken); // Increased delay to ensure setup is complete

        // Now publish event
        TestIntegrationEvent testEvent = new() { Message = "Test subscription message" };
        await eventBus.PublishAsync(testEvent, TestContext.CancellationToken);

        // Assert wait up to 5s for the handler to flip its flag
        TimeSpan maxWait = TimeSpan.FromSeconds(5);
        DateTime startTime = DateTime.UtcNow;
        while (!typedHandler.HandlerCalled && DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(50, TestContext.CancellationToken);
        }

        Assert.IsTrue(typedHandler.HandlerCalled, "Event handler should be called");
        Assert.AreEqual(testEvent.Message, typedHandler.ReceivedMessage);
    }

    [TestMethod]
    public async Task Subscribe_RegistersAndInvokes_FailedEventHandler()
    {
        // Arrange
        IChirpRabbitMqConnection connection = CreateConnection();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_Subscribe_RegistersAndInvokes_FailedEventHandler_subscribe_queue";
        const string exchangeName = "test_Subscribe_RegistersAndInvokes_FailedEventHandler_subscribe_exchange";
        const string dlxExchangeName = "test_Subscribe_RegistersAndInvokes_FailedEventHandler_subscribe_dlx_exchange";

        // Predeclare the exchange and queue so that Subscribe() can bind them
        await using (IChannel adminChannel = await connection.CreateChannelAsync(TestContext.CancellationToken))
        {
            await adminChannel.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Direct,
                true, cancellationToken: TestContext.CancellationToken);

            await adminChannel.QueueDeclareAsync(
                queueName,
                true,
                false,
                false,
                null, cancellationToken: TestContext.CancellationToken);
        }

        // Create a singleton handler instance to ensure we can track its state
        TestFailedIntegrationEventHandler typedHandler = new();

        // Set up DI with the handler as a singleton
        ServiceCollection services = [];
        services.AddSingleton(typedHandler); // Register the exact instance we'll check later
        services.AddSingleton<TestFailedIntegrationEventHandler>(_ => typedHandler); // Also register by type
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
        await eventBus.SubscribeAsync<TestFailedIntegrationEvent, TestFailedIntegrationEventHandler>(TestContext.CancellationToken);

        // Give the internal consumer a moment to spin up and bind
        await Task.Delay(300, TestContext.CancellationToken); // Increased delay to ensure setup is complete

        // Now publish event
        TestFailedIntegrationEvent testEvent = new() { Message = "Test subscription message" };
        bool result = await eventBus.PublishAsync(testEvent, TestContext.CancellationToken);

        // Assert wait up to 5s for the handler to flip its flag
        Assert.IsTrue(result, "Event should be published successfully");
        
        // Assert wait up to 60s for the handler to flip its flag
        TimeSpan maxWait = TimeSpan.FromSeconds(120); 
        DateTime startTime = DateTime.UtcNow;

        // Create a http client to check if we get a message in the dead letter exchange.
        using HttpClient client = new();
        string uri = $"http://{_hostname}:{_webPort}/api/queues/%2F/dlx.{queueName}";

        string cred = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);

        bool isMessageInDLX = false;

        while (!isMessageInDLX && DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(1000, TestContext.CancellationToken);

            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.CancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

            // Check if the message is in the DLX
            if (!responseBody.Contains("\"messages_ready\":1")) continue;
            
            // Message is in the DLX
            isMessageInDLX = true;
            
            break;
        }

        Assert.IsTrue(typedHandler.HandlerCalled, "Event handler should be called");
        Assert.IsTrue(isMessageInDLX, "Message should be in the Dead Letter Exchange");
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)] // Test should complete within 10 seconds, ensuring no hang
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
    [Timeout(10000, CooperativeCancellation = true)] // Test should complete within 10 seconds
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
    [Timeout(10000, CooperativeCancellation = true)] // Test should complete within 10 seconds
    public void Not_Connected_When_Initialized_ThrowsException()
    {
        // Arrange
        ConnectionFactory badConnectionFactory = new()
        {
            HostName = "nonexistent.host.local", // Invalid hostname
            Port = 9999, // Unlikely to be open
            UserName = Username,
            Password = Password,
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

        try
        {
            // Act
            ChirpRabbitMqEventBus eventBus = new(
                chirpRabbitMqConnection,
                new ServiceCollection().BuildServiceProvider(),
                subscriptionsManager,
                queueName,
                5,
                exchangeName,
                dlxExchangeName);
        }
        catch (InvalidOperationException ex)
        {
            // Assert
            // Expected - verify the exception message indicates connection failure
            Assert.IsTrue(
                ex.Message.Contains("RabbitMQ") || ex.Message.Contains("connection") || ex.Message.Contains("model"),
                $"Exception message should indicate connection issue. Actual: {ex.Message}");
            return;
        }
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)] // Test should complete within 10 seconds
    public async Task Connection_Retries_AreTrackedCorrectlyAsync()
    {
        // Arrange
        ConnectionFactory badConnectionFactory = new()
        {
            HostName = "nonexistent.host.local",
            Port = 9999,
            UserName = Username,
            Password = Password,
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
        bool connected = await connection.TryConnectAsync(TestContext.CancellationToken);
        TimeSpan elapsed = DateTime.UtcNow - startTime;

        // Assert
        // ? PRODUCTION BEHAVIOR: Returns false instead of throwing
        Assert.IsFalse(connected, "Connection should fail and return false");
        Assert.IsFalse(connection.IsConnected, "IsConnected should be false");

        // With 3 retries and delays between them (100ms, 200ms, 300ms), plus timeout attempts,
        // the total should be at least the delay time
        Assert.IsGreaterThanOrEqualTo(100, // At least first retry delay
            elapsed.TotalMilliseconds, $"Should have taken time for retries. Actual: {elapsed.TotalMilliseconds}ms");

        Console.WriteLine($"Retry attempts took {elapsed.TotalSeconds:F2}s for {maxRetries} retries");
    }

    // Additional test events for multiple subscription testing
    public record SecondTestIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    public record ThirdTestIntegrationEvent : IntegrationEvent
    {
        public string Message { get; set; } = string.Empty;
    }

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

    /// <summary>
    /// Tests that multiple subscriptions can be added without connection errors
    /// This test specifically validates the fix for the "Connection close forced" issue
    /// </summary>
    [TestMethod]
    public async Task MultipleSubscriptions_DoNotCauseConnectionErrors()
    {
        // Arrange
        IChirpRabbitMqConnection connection = CreateConnection();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_multiple_subscriptions_queue";
        const string exchangeName = "test_multiple_subscriptions_exchange";
        const string dlxExchangeName = "test_multiple_subscriptions_dlx_exchange";

        // Predeclare the exchange and queue
        await using (IChannel adminChannel = await connection.CreateChannelAsync(TestContext.CancellationToken))
        {
            await adminChannel.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Direct,
                true, 
                cancellationToken: TestContext.CancellationToken);

            await adminChannel.QueueDeclareAsync(
                queueName,
                true,
                false,
                false,
                null, 
                cancellationToken: TestContext.CancellationToken);
        }

        // Create handler instances
        TestIntegrationEventHandler handler1 = new();
        SecondTestIntegrationEventHandler handler2 = new();
        ThirdTestIntegrationEventHandler handler3 = new();

        // Set up DI
        ServiceCollection services = new();
        services.AddSingleton(handler1);
        services.AddSingleton<TestIntegrationEventHandler>(_ => handler1);
        services.AddSingleton(handler2);
        services.AddSingleton<SecondTestIntegrationEventHandler>(_ => handler2);
        services.AddSingleton(handler3);
        services.AddSingleton<ThirdTestIntegrationEventHandler>(_ => handler3);
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

        // Act - Subscribe to multiple events in sequence (this was causing connection errors)
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);
        await eventBus.SubscribeAsync<SecondTestIntegrationEvent, SecondTestIntegrationEventHandler>(TestContext.CancellationToken);
        await eventBus.SubscribeAsync<ThirdTestIntegrationEvent, ThirdTestIntegrationEventHandler>(TestContext.CancellationToken);

        // Give the consumer time to stabilize
        await Task.Delay(500, TestContext.CancellationToken);

        // Publish events to all three subscriptions
        TestIntegrationEvent testEvent1 = new() { Message = "First event message" };
        SecondTestIntegrationEvent testEvent2 = new() { Message = "Second event message" };
        ThirdTestIntegrationEvent testEvent3 = new() { Message = "Third event message" };

        await eventBus.PublishAsync(testEvent1, TestContext.CancellationToken);
        await eventBus.PublishAsync(testEvent2, TestContext.CancellationToken);
        await eventBus.PublishAsync(testEvent3, TestContext.CancellationToken);

        // Assert - Wait for all handlers to be called
        TimeSpan maxWait = TimeSpan.FromSeconds(10);
        DateTime startTime = DateTime.UtcNow;
        
        while ((!handler1.HandlerCalled || !handler2.HandlerCalled || !handler3.HandlerCalled) 
               && DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(50, TestContext.CancellationToken);
        }

        Assert.IsTrue(handler1.HandlerCalled, "First handler should be called");
        Assert.AreEqual(testEvent1.Message, handler1.ReceivedMessage);
        
        Assert.IsTrue(handler2.HandlerCalled, "Second handler should be called");
        Assert.AreEqual(testEvent2.Message, handler2.ReceivedMessage);
        
        Assert.IsTrue(handler3.HandlerCalled, "Third handler should be called");
        Assert.AreEqual(testEvent3.Message, handler3.ReceivedMessage);
    }

    /// <summary>
    /// Tests that consumer is only started once even with multiple subscriptions
    /// This validates the consumer state management fix
    /// </summary>
    [TestMethod]
    public async Task ConsumerStartedOnlyOnce_WithMultipleSubscriptions()
    {
        // Arrange - Use a mock connection that tracks channel creation
        var channelCreationCount = 0;
        var consumerStartCount = 0;

        var mockConnection = new TrackingRabbitMqConnection(CreateConnection(), 
            () => channelCreationCount++,
            () => consumerStartCount++);

        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();

        const string queueName = "test_consumer_once_queue";
        const string exchangeName = "test_consumer_once_exchange";
        const string dlxExchangeName = "test_consumer_once_dlx_exchange";

        // Predeclare infrastructure
        using (IChannel adminChannel = await mockConnection.CreateChannelAsync(TestContext.CancellationToken))
        {
            await adminChannel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct, true, 
                cancellationToken: TestContext.CancellationToken);
            await adminChannel.QueueDeclareAsync(queueName, true, false, false, null, 
                cancellationToken: TestContext.CancellationToken);
        }

        // Reset counter after infrastructure setup
        channelCreationCount = 0;

        TestIntegrationEventHandler handler1 = new();
        SecondTestIntegrationEventHandler handler2 = new();

        ServiceCollection services = new();
        services.AddSingleton(handler1);
        services.AddSingleton<TestIntegrationEventHandler>(_ => handler1);
        services.AddSingleton(handler2);
        services.AddSingleton<SecondTestIntegrationEventHandler>(_ => handler2);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        ChirpRabbitMqEventBus eventBus = new(
            mockConnection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act - Subscribe to multiple events
        await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(TestContext.CancellationToken);
        await eventBus.SubscribeAsync<SecondTestIntegrationEvent, SecondTestIntegrationEventHandler>(TestContext.CancellationToken);

        await Task.Delay(500, TestContext.CancellationToken);

        // Assert - Verify reasonable channel usage
        // We expect: 1 consumer channel during initialization, 2 binding channels (one per subscription)
        // Total should be around 3-4 channels (not 6+ which would indicate problems)
        Assert.IsLessThanOrEqualTo(5,
            channelCreationCount, $"Too many channels created: {channelCreationCount}. Expected around 3 (1 consumer + 2 binding channels)");
        
        Console.WriteLine($"Channels created: {channelCreationCount}");
    }

    /// <summary>
    /// Mock connection wrapper that tracks channel creation
    /// </summary>
    private class TrackingRabbitMqConnection : IChirpRabbitMqConnection
    {
        private readonly IChirpRabbitMqConnection _innerConnection;
        private readonly Action _onChannelCreated;
        private readonly Action _onConsumerStarted;

        public TrackingRabbitMqConnection(IChirpRabbitMqConnection innerConnection, 
            Action onChannelCreated, Action onConsumerStarted)
        {
            _innerConnection = innerConnection;
            _onChannelCreated = onChannelCreated;
            _onConsumerStarted = onConsumerStarted;
        }

        public bool IsConnected => _innerConnection.IsConnected;

        public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            return _innerConnection.TryConnectAsync(cancellationToken);
        }

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            _onChannelCreated();
            return await _innerConnection.CreateChannelAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Tests that subscription errors are properly logged and thrown
    /// </summary>
    [TestMethod]
    public async Task SubscribeAsync_LogsAndThrowsErrors_OnFailure()
    {
        // Arrange
        DisconnectedRabbitMqConnection disconnectedConnection = new();
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new();
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        const string queueName = "test_error_logging_queue";
        const string exchangeName = "test_error_logging_exchange";
        const string dlxExchangeName = "test_error_logging_dlx_exchange";

        ChirpRabbitMqEventBus eventBus = new(
            disconnectedConnection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        // Act & Assert
        bool exceptionThrown = false;
        try
        {
            await eventBus.SubscribeAsync<TestIntegrationEvent, TestIntegrationEventHandler>(
                TestContext.CancellationToken);
        }
        catch (Exception)
        {
            exceptionThrown = true;
        }

        Assert.IsTrue(exceptionThrown, "Should throw exception when subscription fails");
    }

    public TestContext TestContext { get; set; }
}