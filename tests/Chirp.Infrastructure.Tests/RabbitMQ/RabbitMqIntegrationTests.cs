using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMqIntegrationTests
{
    private RabbitMqContainer? _rabbitMqContainer;
    private RabbitMqEventBus? _publisherEventBus;
    private RabbitMqEventBus? _subscriberEventBus;
    private IRabbitMqConnection? _rabbitMqConnection;
    private ServiceProvider? _serviceProvider;

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
        var connectionFactory = new ConnectionFactory()
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

        // Setup DI for test handlers
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TestEventReceivedHandler>();
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }

        _serviceProvider?.Dispose();
    }

    [TestMethod]
    public async Task PublishAndSubscribe_WhenEventIsPublished_HandlerReceivesMessage()
    {
        // Arrange
        const string exchangeName = "test_integration_exchange";
        const string subscriberQueue = "subscriber_integration_queue";
        const string publisherQueue = "publisher_integration_queue";

        var subscriberSubscriptionManager = new InMemoryEventBusSubscriptionsManager();
        var publisherSubscriptionManager = new InMemoryEventBusSubscriptionsManager();

        // Create event buses with different queue names to simulate separate services
        _subscriberEventBus = new RabbitMqEventBus(
            _rabbitMqConnection,
            _serviceProvider,
            subscriberSubscriptionManager,
            subscriberQueue,
            5,
            exchangeName,
            "test_dlx_exchange");

        _publisherEventBus = new RabbitMqEventBus(
            _rabbitMqConnection,
            _serviceProvider,
            publisherSubscriptionManager,
            publisherQueue,
            5,
            exchangeName,
            "test_dlx_exchange");

        // Get the handler from DI
        var handler = _serviceProvider?.GetRequiredService<TestEventReceivedHandler>();
        Assert.IsNotNull(handler);

        // Set up the message we expect to send
        var expectedMessage = "Hello, RabbitMQ!";
        var messageReceivedSignal = new SemaphoreSlim(0, 1);
        handler.SetupExpectedMessage(expectedMessage, messageReceivedSignal);

        // Subscribe to the event
        _subscriberEventBus?.Subscribe<TestMessageEvent, TestEventReceivedHandler>();

        // Give the subscription time to establish
        await Task.Delay(1000);

        // Act - Publish the test event
        var testEvent = new TestMessageEvent { Message = expectedMessage };
        _publisherEventBus?.Publish(testEvent);

        Debug.WriteLine("Message published, waiting for handler to process it...");

        // Wait for the message to be received, with timeout
        var messageReceived = await messageReceivedSignal.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.IsTrue(messageReceived, "Message was not received within timeout period");
        Assert.AreEqual(expectedMessage, handler.ReceivedMessage);
    }

    // Test event class - using correct record inheritance syntax
    public record TestMessageEvent() : IntegrationEvent
    {
        public string? Message { get; set; }
    }

    // Test event handler
    public class TestEventReceivedHandler : IIntegrationEventHandler<TestMessageEvent>
    {
        private string? _expectedMessage;
        private SemaphoreSlim? _messageReceivedSignal;
        
        public string? ReceivedMessage { get; private set; }

        public void SetupExpectedMessage(string expectedMessage, SemaphoreSlim messageReceivedSignal)
        {
            _expectedMessage = expectedMessage;
            _messageReceivedSignal = messageReceivedSignal;
        }

        public Task Handle(TestMessageEvent @event)
        {
            Debug.WriteLine($"TestEventReceivedHandler received message: {@event.Message}");
            ReceivedMessage = @event.Message;
            
            if (_expectedMessage == ReceivedMessage && _messageReceivedSignal != null)
            {
                _messageReceivedSignal.Release();
            }
            
            return Task.CompletedTask;
        }
    }
}