# Chirp - Flexible Messaging Library

![Chirp Logo](https://via.placeholder.com/150x150?text=Chirp)

[![NuGet](https://img.shields.io/nuget/v/Chirp.svg)](https://www.nuget.org/packages/Chirp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Chirp is a flexible, provider-agnostic messaging library that simplifies publishing and consuming messages across various message brokers, including RabbitMQ, Kafka, Redis, Azure Service Bus, Amazon SQS, NATS, and Google PubSub.

## Features

- **Provider-agnostic** API for messaging operations
- **Unified interface** for multiple message brokers
- **Simple integration** with dependency injection
- **Support for multiple message brokers**:
  - RabbitMQ (fully implemented)
  - Kafka (planned)
  - Redis (planned)
  - Azure Service Bus (planned)
  - Amazon SQS (planned)
  - NATS (planned)
  - Google PubSub (planned)
- **Message retries** with configurable retry counts
- **Dead letter exchange/queue** support for failed messages
- **Clean subscription management** with in-memory event tracking

## Installation

```bash
dotnet add package Chirp
```

## Getting Started

### Configuration

Add the necessary configuration to your appsettings.json:

```json
{
  "RMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "ExchangeName": "chirp_exchange",
    "ExchangeNameDLX": "chirp_dlx_exchange"
  }
}
```

### Setting Up Dependencies

Register the required dependencies in your `Program.cs` or `Startup.cs`:

```csharp
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Add Chirp services with RabbitMQ implementation
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
});
```

### Creating Events

Create event classes that inherit from `IntegrationEvent`:

```csharp
using Chirp.Domain.Common;

public record OrderCreatedEvent(int OrderId, string CustomerName, decimal Total) : IntegrationEvent;
```

### Creating Event Handlers

Create handlers that implement `IIntegrationEventHandler<T>`:

```csharp
using Chirp.Application.Interfaces;

public class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Process the event
        Console.WriteLine($"Order {event.OrderId} created for {event.CustomerName} with total {event.Total}");
        await Task.CompletedTask;
    }
}
```

### Publishing Events

Inject `IEventBus` and publish events:

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;

    public OrderService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void CreateOrder(int orderId, string customerName, decimal total)
    {
        // Create and publish the event
        var orderCreatedEvent = new OrderCreatedEvent(orderId, customerName, total);
        _eventBus.Publish(orderCreatedEvent);
    }
}
```

### Subscribing to Events

Subscribe to events in your application startup:

```csharp
public class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        
        // Subscribe to events
        eventBus.Subscribe<OrderCreatedEvent, OrderCreatedEventHandler>();
    }
}
```

## Advanced Usage

### Using the Event Bus Factory

You can also use the `EventBusFactory` to create an instance of the event bus:

```csharp
IEventBus eventBus = EventBusFactory.Create(
    EventBusType.RabbitMQ,
    serviceProvider,
    configuration,
    "my_service_queue",
    retryCount: 5);
```

### Working with Multiple Event Bus Implementations

If your application needs to work with multiple message brokers:

```csharp
// Configure RabbitMQ
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "rabbitmq_queue";
});

// Add Redis (when implemented)
services.AddChirpRedis(options =>
{
    options.ChannelName = "redis_channel";
});
```

## Supported Message Brokers

| Provider | Status | Configuration Section |
|----------|--------|------------------------|
| RabbitMQ | ? Implemented | `RMQ` |
| Kafka | ?? Planned | `Kafka` |
| Redis | ?? Planned | `Redis` |
| Azure Service Bus | ?? Planned | `AzureServiceBus` |
| Amazon SQS | ?? Planned | `AWS:SQS` |
| NATS | ?? Planned | `NATS` |
| Google PubSub | ?? Planned | `GooglePubSub` |

## Contributing

Contributions are welcome! Feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Author

Created by [Andrew Eberle](https://github.com/andreweberle)