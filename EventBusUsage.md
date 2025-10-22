# Chirp Event Bus Usage Guide

## Setting up the Event Bus with Automatic Consumer Registration

Chirp now supports automatic registration and subscription of event consumers using the `AddConsumer<T>` method. This
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

Event handlers should implement the `IIntegrationEventHandler<T>` interface:

```csharp
public class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreatedEvent>
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
   `IIntegrationEventHandler<T>` interface.
3. **Dependency Injection**: Handlers still get all dependencies injected automatically.
4. **Automatic Subscription**: Handlers are automatically subscribed to the event bus - no manual subscription required.

## Example with Multiple Events per Handler

A single handler can handle multiple event types by implementing multiple interfaces:

```csharp
public class OrderProcessingHandler : 
    IIntegrationEventHandler<OrderCreatedEvent>,
    IIntegrationEventHandler<OrderUpdatedEvent>,
    IIntegrationEventHandler<OrderCanceledEvent>
{
    // Constructor with dependencies
    
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Handle order creation
    }
    
    public async Task Handle(OrderUpdatedEvent @event)
    {
        // Handle order updates
    }
    
    public async Task Handle(OrderCanceledEvent @event)
    {
        // Handle order cancellation
    }
}
```

Register it once and it will be subscribed to all event types:

```csharp
options.AddConsumer<OrderProcessingHandler>();
```

## Advanced Configuration

For more advanced scenarios, you can still access the underlying event bus directly:

```csharp
// Get the event bus
var eventBus = serviceProvider.GetRequiredService<IEventBus>();

// Publish an event
eventBus.Publish(new OrderCreatedEvent { OrderId = "12345" });
```
