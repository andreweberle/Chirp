# Chirp - Flexible Messaging Library

[comment]: <![Chirp Logo](https://via.placeholder.com/150x150?text=Chirp)>

[![NuGet](https://img.shields.io/nuget/v/Chirp.svg)](https://www.nuget.org/packages/Chirp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Chirp is a flexible, provider-agnostic messaging library that simplifies publishing and consuming messages across various message brokers, including RabbitMQ, Kafka, Redis, Azure Service Bus, Amazon SQS, NATS, and Google PubSub.

## Features

- **Provider-agnostic** API for messaging operations
- **Unified interface** for multiple message brokers
- **Simple integration** with dependency injection
- **Automatic handler registration and subscription**
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
`dotnet add package Chirp`
## Getting Started

### Configuration

Add the necessary configuration to your `appsettings.json`:{
  "RMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "ExchangeName": "chirp_exchange",
    "ExchangeNameDLX": "chirp_dlx_exchange"
  }
}### Setting Up Dependencies
Register the required dependencies in your `Program.cs` or `Startup.cs`:using Chirp.Infrastructure;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Add Chirp services with RabbitMQ implementation
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
    
    // Register event handlers - they'll be automatically subscribed
    options.AddConsumer<OrderCreatedEventHandler>();
    options.AddConsumer<PaymentReceivedEventHandler>();
});### Creating Events

Create event classes that inherit from `IntegrationEvent`:using Chirp.Domain.Common;

public record OrderCreatedEvent(int OrderId, string CustomerName, decimal Total) : IntegrationEvent;### Creating Event Handlers

Create handlers that implement `IIntegrationEventHandler<T>`:using Chirp.Application.Interfaces;

public class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Process the event
        Console.WriteLine($"Order {event.OrderId} created for {event.CustomerName} with total {event.Total}");
        await Task.CompletedTask;
    }
}### Publishing Events

Inject `IEventBus` and publish events:public class OrderService
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
}## Advanced Usage

### Using the Event Bus Factory

You can also use the `EventBusFactory` to create an instance of the event bus:IEventBus eventBus = EventBusFactory.Create(
    EventBusType.RabbitMQ,
    serviceProvider,
    configuration,
    "my_service_queue",
    retryCount: 5);
### Working with Multiple Message Brokers

Chirp is designed to support multiple message broker implementations. This can be useful in scenarios like:

- Migrating from one messaging system to another
- Creating hybrid systems with different messaging needs
- Publishing to multiple brokers for redundancy
- Consuming messages from different sources

Currently, the library has a fully implemented RabbitMQ provider, with other providers planned for future releases. 
Once additional providers are implemented, you'll be able to use them by registering the appropriate connections and event buses.

To manually register multiple event bus instances (once additional providers are implemented):// Register the default event bus (RabbitMQ)
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "primary_queue";
});

// Example of how you might register additional event buses in the future
// Note: This is for illustration purposes only and will work when other providers are implemented
/*
// Register Redis connection
services.AddSingleton<IRedisConnection>(sp => 
{
    // Configure Redis connection
    return new RedisConnection(configuration);
});

// Register another event bus
services.AddSingleton<IEventBus>(serviceProvider => 
{
    return EventBusFactory.Create(
        EventBusType.Redis, 
        serviceProvider,
        configuration,
        "redis-channel"
    );
});
*/#### Planned Features for Multi-Broker Support

- Message routing based on event type
- Automatic failover between brokers
- Unified configuration for multiple brokers
- Message synchronization between different broker types

## Supported Message Brokers

| Provider                 | Status          | Configuration Section    |
| :----------------------- | :-------------: | :----------------------- |
|  **RabbitMQ**            | ✅ Implemented | ``RMQ``                  |
| **Kafka**                |  Planned        | ``Kafka``                |
| **Redis**                |  Planned        | ``Redis``                |
| **Azure Service Bus**    | Planned         | ``AzureServiceBus``      |
| **Amazon SQS**           | Planned         | ``AWS:SQS``              |
| **NATS**                 | Planned         | ``NATS``                 |
| **Google Pub/Sub**       | Planned         | ``GooglePubSub``         |


## Contributing

Contributions are welcome! Feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.