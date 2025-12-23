# Chirp Event Bus Usage Guide

## Setting up the Event Bus with Automatic Consumer Registration

Chirp supports automatic registration and subscription of event consumers using the `AddConsumer<T>` method. This
eliminates the need to manually add each handler as a transient service or manually subscribe them.

### Register your event bus and handlers

```csharp
// In your startup or program.cs
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
    
    // Register your event handlers - they'll be automatically subscribed
    options.AddConsumer<OrderCreatedEventHandler>();
    options.AddConsumer<PaymentReceivedEventHandler>();
    options.AddConsumer<ShipmentReadyEventHandler>();
});
```

That's it! No additional setup is required. The event handlers will be automatically subscribed to the event bus when
it's first resolved from the service provider.

## Creating Event Handlers

Event handlers should implement the `IChirpIntegrationEventHandler<T>` interface:

```csharp
using Chirp.Application.Interfaces;

public class OrderCreatedEventHandler : IChirpIntegrationEventHandler<OrderCreatedEvent>
{
    private readonly IOrderService _orderService;
    
    public OrderCreatedEventHandler(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Process the event
        await _orderService.ProcessNewOrder(@event.OrderId);
    }
}
```

## Benefits of using AddConsumer<T>

1. **Simplified Registration**: No need to manually register each handler as a transient service.
2. **Automatic Interface Registration**: The system automatically registers the handler with the correct
   `IChirpIntegrationEventHandler<T>` interface.
3. **Dependency Injection**: Handlers still get all dependencies injected automatically.
4. **Automatic Subscription**: Handlers are automatically subscribed to the event bus - no manual subscription required.

## Example with Multiple Events per Handler

A single handler can handle multiple event types by implementing multiple interfaces:

```csharp
public class OrderProcessingHandler : 
    IChirpIntegrationEventHandler<OrderCreatedEvent>,
    IChirpIntegrationEventHandler<OrderUpdatedEvent>,
    IChirpIntegrationEventHandler<OrderCanceledEvent>
{
    private readonly IOrderService _orderService;
    
    public OrderProcessingHandler(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Handle order creation
        await _orderService.CreateOrder(@event);
    }
    
    public async Task Handle(OrderUpdatedEvent @event)
    {
        // Handle order updates
        await _orderService.UpdateOrder(@event);
    }
    
    public async Task Handle(OrderCanceledEvent @event)
    {
        // Handle order cancellation
        await _orderService.CancelOrder(@event);
    }
}
```

Register it once and it will be subscribed to all event types:

```csharp
options.AddConsumer<OrderProcessingHandler>();
```

## Using Strongly-Typed Options

For better type safety and IntelliSense support, you can use provider-specific options:

### RabbitMQ with Strongly-Typed Options

```csharp
using Chirp.Application.Common.EventBusOptions;

services.AddChirp(options =>
{
    // RabbitMQ-specific configuration
    options.Host = "localhost";
    options.Port = 5672;
    options.Username = "guest";
    options.Password = "guest";
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
    options.ExchangeName = "my_exchange";
    options.DeadLetterExchangeName = "my_dlx";
    options.QueueDurable = true;
    options.PersistentMessages = true;
    
    // Register handlers
    options.AddConsumer<OrderCreatedEventHandler>();
});
```

## Publishing Events

### Using Dependency Injection

```csharp
public class OrderService
{
    private readonly IChirpEventBus _eventBus;

    public OrderService(IChirpEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task CreateOrderAsync(int orderId, string customerName, decimal total)
    {
        // Create and publish the event
        var orderCreatedEvent = new OrderCreatedEvent(orderId, customerName, total);
        await _eventBus.PublishAsync(orderCreatedEvent);
    }
}
```

### Direct Event Bus Access

For more advanced scenarios, you can access the underlying event bus directly:

```csharp
// Get the event bus
var eventBus = serviceProvider.GetRequiredService<IChirpEventBus>();

// Publish an event
await eventBus.PublishAsync(new OrderCreatedEvent 
{ 
    OrderId = 12345,
    CustomerName = "John Doe",
    Total = 99.99m
});
```

## Message Broker Support

Currently, **RabbitMQ** is the only fully implemented provider. Other message brokers have scaffolding in place but are not yet functional:

- ? **RabbitMQ** - Fully implemented and tested
- ?? **Kafka** - Scaffolding in place, implementation pending
- ?? **Redis** - Scaffolding in place, implementation pending
- ?? **Azure Service Bus** - Scaffolding in place, implementation pending
- ?? **Amazon SQS** - Scaffolding in place, implementation pending
- ?? **NATS** - Scaffolding in place, implementation pending
- ?? **Google Pub/Sub** - Scaffolding in place, implementation pending

When using non-RabbitMQ providers, you'll encounter `NotImplementedException` until those implementations are completed.

## Error Handling

Chirp includes built-in retry logic and dead-letter handling for failed messages:

```csharp
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "my_service_queue";
    options.RetryCount = 3; // Retry failed messages 3 times
    options.DeadLetterExchangeName = "my_dlx"; // Dead letter exchange for permanently failed messages
    
    options.AddConsumer<OrderCreatedEventHandler>();
});
```

## Best Practices

1. **Keep Handlers Focused**: Each handler should handle one specific event type or related events
2. **Use Async/Await**: All handler methods are async - use await for I/O operations
3. **Handle Failures Gracefully**: Implement proper error handling in your handlers
4. **Use Dependency Injection**: Inject services into your handlers rather than creating instances directly
5. **Test Handlers**: Write unit tests for your event handlers to ensure correct behavior
6. **Monitor Dead Letter Queues**: Regularly check dead letter queues for permanently failed messages

## Advanced Configuration

### Custom Subscription Manager

If you need custom subscription management logic, you can implement `IChirpEventBusSubscriptionsManager`:

```csharp
public class CustomSubscriptionManager : IChirpEventBusSubscriptionsManager
{
    // Implement custom subscription logic
}

// Register in DI
services.AddSingleton<IChirpEventBusSubscriptionsManager, CustomSubscriptionManager>();
```

### Multiple Event Buses

While not yet fully supported, the architecture allows for multiple event bus instances:

```csharp
// Primary event bus (RabbitMQ)
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "primary_queue";
});

// Future: Secondary event bus (different provider)
// This will be supported once additional providers are implemented
