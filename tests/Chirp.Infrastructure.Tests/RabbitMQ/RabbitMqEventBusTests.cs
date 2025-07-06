using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;
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

        public void TryConnect()
        {
            _connection = _connectionFactory.CreateConnection();
        }

        public IModel CreateModel()
        {
            if (!IsConnected) TryConnect();
            return _connection!.CreateModel();
        }
    }

    private static TestRabbitMqConnection CreateConnection()
    {
        ConnectionFactory connectionFactory = new ConnectionFactory
        {
            HostName = _hostname,
            Port = _port,
            UserName = Username,
            Password = Password,
            DispatchConsumersAsync = true,
        };

        return new TestRabbitMqConnection(connectionFactory);
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
        ChirpRabbitMqEventBus eventBus = new ChirpRabbitMqEventBus(
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
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new InMemoryEventBusSubscriptionsManager();
        ServiceCollection serviceCollection = new ServiceCollection();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        
        const string queueName = "test_publish_queue";
        const string exchangeName = "test_publish_exchange";
        const string dlxExchangeName = "test_publish_dlx_exchange";
        
        ChirpRabbitMqEventBus eventBus = new ChirpRabbitMqEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            5,
            exchangeName,
            dlxExchangeName);

        TestIntegrationEvent testEvent = new TestIntegrationEvent { Message = "Test message" };
        bool messageReceived = false;
        string? receivedMessage = null;
        
        // Set up a consumer to verify the message was published
        using IModel channel = connection.CreateModel();
        
        // Make sure the queue exists and is bound to the exchange
        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true);
        channel.QueueDeclare(queueName, true, false, false, null);
        channel.QueueBind(queueName, exchangeName, testEvent.GetType().Name);
        
        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
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
        InMemoryEventBusSubscriptionsManager subscriptionsManager = new InMemoryEventBusSubscriptionsManager();

        const string queueName = "test_subscribe_queue";
        const string exchangeName = "test_subscribe_exchange";
        const string dlxExchangeName = "test_subscribe_dlx_exchange";

        // Predeclare the exchange and queue so that Subscribe() can bind them
        using (IModel adminChannel = connection.CreateModel())
        {
            adminChannel.ExchangeDeclare(
                exchange: exchangeName,
                type: ExchangeType.Direct,
                durable: true);

            adminChannel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        // Create a singleton handler instance to ensure we can track its state
        TestIntegrationEventHandler typedHandler = new TestIntegrationEventHandler();
        
        // Set up DI with the handler as a singleton
        ServiceCollection services = new();
        services.AddSingleton(typedHandler); // Register the exact instance we'll check later
        services.AddSingleton<TestIntegrationEventHandler>(_ => typedHandler); // Also register by type
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Create the bus
        ChirpRabbitMqEventBus eventBus = new ChirpRabbitMqEventBus(
            connection,
            serviceProvider,
            subscriptionsManager,
            queueName,
            retryMax: 5,
            exchangeName: exchangeName,
            dlxExchangeName: dlxExchangeName);

        // Act
        eventBus.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

        // Give the internal consumer a moment to spin up and bind
        await Task.Delay(300); // Increased delay to ensure setup is complete

        // Now publish event
        TestIntegrationEvent testEvent = new TestIntegrationEvent { Message = "Test subscription message" };
        eventBus.Publish(testEvent);

        // Assert — wait up to 5s for the handler to flip its flag
        TimeSpan maxWait = TimeSpan.FromSeconds(5);
        DateTime startTime = DateTime.UtcNow;
        while (!typedHandler.HandlerCalled && DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(50);
        }

        Assert.IsTrue(typedHandler.HandlerCalled, "Event handler should be called");
        Assert.AreEqual(testEvent.Message, typedHandler.ReceivedMessage);
    }
}