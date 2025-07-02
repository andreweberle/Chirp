using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Testcontainers.RabbitMq;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

namespace Chirp.Infrastructure.Tests.RabbitMQ;

[TestClass]
public class RabbitMqChannelExceptionTests
{
    private RabbitMqContainer? _rabbitMqContainer;
    private IRabbitMqConnection? _rabbitMqConnection;
    private ServiceProvider? _serviceProvider;
    private const string ExchangeName = "test_exception_exchange";
    private const string QueueName = "test_exception_queue";

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
        var connectionFactory = new ConnectionFactory
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
        serviceCollection.AddSingleton<TestExceptionEventHandler>();
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
    public async Task ChannelCallbackException_WhenTriggered_RecreatesChannelAndContinuesConsuming()
    {
        // Arrange
        var messageReceivedSignal = new SemaphoreSlim(0, 1);
        var subscriptionManager = new InMemoryEventBusSubscriptionsManager();

        // Set up the handler
        var handler = _serviceProvider?.GetRequiredService<TestExceptionEventHandler>();
        Assert.IsNotNull(handler);
        handler.SetupSignal(messageReceivedSignal);

        // Create the event bus with our custom callback exception trigger 
        var eventBus = new RabbitMqEventBusWithCallbackException(
            _rabbitMqConnection,
            _serviceProvider,
            subscriptionManager,
            QueueName,
            5,
            ExchangeName,
            "test_dlx_exchange");

        // Subscribe to the event
        eventBus.Subscribe<TestExceptionEvent, TestExceptionEventHandler>();

        // Give the subscription time to establish
        await Task.Delay(1000);

        // Trigger a callback exception
        eventBus.TriggerCallbackException();

        // Wait a moment for the channel to be recreated
        await Task.Delay(1000);

        // Act - publish a message after the exception
        var testEvent = new TestExceptionEvent { Message = "After exception" };
        eventBus.Publish(testEvent);

        // Wait for the message to be received
        var messageReceived = await messageReceivedSignal.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.IsTrue(messageReceived, "Message was not received after channel exception");
        Assert.AreEqual("After exception", handler.ReceivedMessage);
    }

    // Custom EventBus class that allows us to trigger a callback exception for testing
    private class RabbitMqEventBusWithCallbackException : RabbitMqEventBus
    {
        public RabbitMqEventBusWithCallbackException(
            IRabbitMqConnection rabbitMQConnection,
            IServiceProvider serviceProvider,
            IEventBusSubscriptionsManager eventBusSubscriptionsManager,
            string? queueName = null,
            int retryMax = 5,
            string exchangeName = "chirp_event_bus",
            string dlxExchangeName = "_dlxExchangeName") 
            : base(rabbitMQConnection, serviceProvider, eventBusSubscriptionsManager, queueName, retryMax, exchangeName, dlxExchangeName)
        {
        }

        public void TriggerCallbackException()
        {
            // Access the protected consumer channel through reflection
            var channelField = typeof(RabbitMqEventBus).GetField("_consumerChannel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (channelField != null)
            {
                var channel = channelField.GetValue(this) as IModel;
                if (channel != null)
                {
                    // Simulate a channel exception by closing it abruptly
                    try
                    {
                        // This will trigger the callback exception event
                        channel.Close(404, "Simulated exception for testing");
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception during test: {ex.Message}");
                    }
                }
            }
        }
    }

    // Test event class
    public record TestExceptionEvent() : IntegrationEvent
    {
        public string? Message { get; set; }
    }

    // Test event handler
    public class TestExceptionEventHandler : IIntegrationEventHandler<TestExceptionEvent>
    {
        private SemaphoreSlim? _messageReceivedSignal;
        public string? ReceivedMessage { get; private set; }

        public void SetupSignal(SemaphoreSlim signal)
        {
            _messageReceivedSignal = signal;
        }

        public Task Handle(TestExceptionEvent @event)
        {
            ReceivedMessage = @event.Message;
            _messageReceivedSignal?.Release();
            return Task.CompletedTask;
        }
    }
}                             