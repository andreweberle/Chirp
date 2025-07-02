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
public class RabbitMqErrorHandlingTests
{
    private RabbitMqContainer? _rabbitMqContainer;
    private RabbitMqEventBus? _eventBus;
    private IRabbitMqConnection? _rabbitMqConnection;
    private ServiceProvider? _serviceProvider;
    private const string ExchangeName = "test_error_exchange";
    private const string DlxExchangeName = "test_error_dlx_exchange";
    private const string QueueName = "test_error_queue";
    private const string DlxQueueName = "dlx.test_error_queue";
    private const int MaxRetries = 2; // Reduce retry count for faster testing
    private IModel? _monitorChannel;
    private SemaphoreSlim _dlxMessageReceivedSignal = new SemaphoreSlim(0, 1);
    private string? _dlxMessageContent;

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
        serviceCollection.AddTransient<FailingEventHandler>();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        // Create the event bus with limited retries for faster testing
        _eventBus = new RabbitMqEventBus(
            _rabbitMqConnection,
            _serviceProvider,
            new InMemoryEventBusSubscriptionsManager(),
            QueueName,
            MaxRetries,
            ExchangeName,
            DlxExchangeName);
            
        // Setup a separate channel to monitor the DLX queue
        SetupDlxMonitor();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        // Clean up the DLX monitor
        if (_monitorChannel != null)
        {
            try 
            {
                if (_monitorChannel.IsOpen)
                {
                    _monitorChannel.Close();
                }
                _monitorChannel.Dispose();
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Error closing monitor channel: {ex.Message}");
            }
        }
        
        // Dispose the event bus
        _eventBus?.Dispose();
        
        // Dispose the service provider
        _serviceProvider?.Dispose();
        
        // Dispose the signal
        _dlxMessageReceivedSignal.Dispose();
        
        // Stop the container last
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }
    
    private void SetupDlxMonitor()
    {
        try
        {
            // Create a new channel for monitoring the DLX queue
            _monitorChannel = _rabbitMqConnection?.CreateModel();
            if (_monitorChannel == null) return;
            
            // Ensure the DLX exchange and queue exist
            _monitorChannel.ExchangeDeclare(DlxExchangeName, ExchangeType.Direct, true);
            _monitorChannel.QueueDeclare(DlxQueueName, true, false, false, null);
            _monitorChannel.QueueBind(DlxQueueName, DlxExchangeName, DlxQueueName);
            
            // Create a consumer for the DLX queue
            var consumer = new EventingBasicConsumer(_monitorChannel);
            consumer.Received += (sender, args) =>
            {
                try
                {
                    _dlxMessageContent = Encoding.UTF8.GetString(args.Body.Span);
                    _monitorChannel.BasicAck(args.DeliveryTag, false);
                    _dlxMessageReceivedSignal.Release();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling DLX message: {ex.Message}");
                }
            };
            
            // Start consuming from the DLX queue
            _monitorChannel.BasicConsume(DlxQueueName, false, consumer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up DLX monitor: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MessageProcessing_WhenHandlerFails_MessageEndsUpInDeadLetterQueue()
    {
        // Arrange
        Assert.IsNotNull(_rabbitMqConnection, "RabbitMQ connection should be initialized");
        Assert.IsNotNull(_eventBus, "Event bus should be initialized");
        
        // Subscribe to the test event with a handler that always fails
        _eventBus.Subscribe<TestErrorEvent, FailingEventHandler>();

        // Give the subscription time to establish
        await Task.Delay(1000);

        // Act - Publish a test event that will cause the handler to fail
        var testEvent = new TestErrorEvent { Message = "This will fail" };
        _eventBus.Publish(testEvent);

        Console.WriteLine("Test event published, waiting for it to reach dead letter queue...");

        // Wait for the message to reach the dead letter queue after retries
        // Calculated wait time: base time + (retry delay * retries)
        // For MaxRetries=2: ~5-7 seconds should be enough
        var messageReceived = await _dlxMessageReceivedSignal.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.IsTrue(messageReceived, "Message was not received in dead letter queue");
        Assert.IsNotNull(_dlxMessageContent, "Message content from dead letter queue should not be null");
        
        // Parse and verify message content
        Assert.IsTrue(_dlxMessageContent!.Contains("This will fail"), 
            $"Dead letter message should contain the original message content. Actual: {_dlxMessageContent}");
    }

    // Test event class for error testing - using correct record inheritance syntax
    public record TestErrorEvent() : IntegrationEvent
    {
        public string? Message { get; set; }
    }

    // Handler that always throws an exception
    public class FailingEventHandler : IIntegrationEventHandler<TestErrorEvent>
    {
        private static int _callCount = 0;

        public Task Handle(TestErrorEvent @event)
        {
            _callCount++;
            Console.WriteLine($"FailingEventHandler called {_callCount} times");
            
            // Always throw an exception
            throw new InvalidOperationException($"Simulated failure processing event: {@event.Message}");
        }
    }
}