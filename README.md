﻿# Chirp - Flexible Messaging Library

[comment]: <![Chirp Logo](https://via.placeholder.com/150x150?text=Chirp)>

[![NuGet](https://img.shields.io/nuget/v/Chirp.svg)](https://www.nuget.org/packages/Chirp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Chirp is a flexible, provider-agnostic messaging library that simplifies publishing and consuming messages across
various message brokers, including `RabbitMQ`, with planned support for `Kafka`, `Redis`, `Azure Service Bus`, `Amazon SQS`, `NATS`, and `Google Pub/Sub`.

## Features

- **Provider-agnostic** API for messaging operations
- **Unified interface** for multiple message brokers
- **Simple integration** with dependency injection
- **Automatic handler registration and subscription**
- **Strongly-typed configuration options** for each message broker
- **Message retries** with configurable retry counts
- **Dead letter exchange/queue** support for failed messages (RabbitMQ & InMemory)
- **Clean subscription management** with in-memory event tracking
- **Support for multiple message brokers**:
    - RabbitMQ (✅ fully implemented)
    - Kafka (🚧 scaffolding in place)
    - Redis (🚧 scaffolding in place)
    - Azure Service Bus (🚧 scaffolding in place)
    - Amazon SQS (🚧 scaffolding in place)
    - NATS (🚧 scaffolding in place)
    - Google Pub/Sub (🚧 scaffolding in place)

## Installation

`dotnet add package Chirp`

## Getting Started

### Configuration

Add the necessary configuration to your `appsettings.json`:

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
using Chirp.Infrastructure;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Add Chirp services with RabbitMQ
services.AddChirp(options =>
{
    options.EventBusType = EventBusType.RabbitMQ;
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
    
    // Register event handlers - they'll be automatically subscribed
    options.AddConsumer<OrderCreatedEventHandler>();
    options.AddConsumer<PaymentReceivedEventHandler>();
});
```

#### Using Strongly Typed Options

Chirp supports strongly typed configuration options for each message broker implementation. This provides better
IntelliSense and type safety:

##### RabbitMQ Configuration

```csharp
using Chirp.Application.Common.EventBusOptions;
using Chirp.Infrastructure;

// Add Chirp services with RabbitMQ-specific options
services.AddChirp(options => 
{
    options.Host = "my-rabbitmq-server";
    options.Username = "my-username";
    options.Password = "my-password";
    options.QueueName = "my_service_queue";
    options.RetryCount = 3;
    options.ExchangeName = "my_custom_exchange";
    options.DeadLetterExchangeName = "my_custom_dlx";
    options.QueueDurable = true;
    options.PersistentMessages = true;
    
    // Register event handlers
    options.AddConsumer<OrderCreatedEventHandler>();
    options.AddConsumer<PaymentReceivedEventHandler>();
});
```

##### InMemory Configuration (Testing/Development)

```csharp
using Chirp.Application.Common.EventBusOptions;
using Chirp.Infrastructure;

// Add Chirp services with InMemory provider
services.AddChirp(options => 
{
    // Configure InMemory options
    options.QueueName = "my_memory_queue";
    options.RetryCount = 3;
    
    // Register event handlers
    options.AddConsumer<OrderCreatedEventHandler>();
});
```

> **Note:** The InMemory provider includes a Dead Letter Queue for failed messages. We are currently developing an API to retrieve, inspect, and replay these messages programmatically.

#### Future Provider Configuration Examples

The following configuration examples show how other providers will be configured once implemented:

**Kafka Configuration (Scaffolding in place)**

```csharp
using Chirp.Application.Common.EventBusOptions;
using Chirp.Infrastructure;

// Note: Kafka implementation is not yet complete
services.AddChirp(options => 
{
    options.TopicName = "my-topic";
    options.ConsumerGroupId = "my-service-group";
    options.AutoCreateTopics = true;
    options.NumPartitions = 3;
    options.ReplicationFactor = 2;
    
    // Register event handlers
    options.AddConsumer<OrderCreatedEventHandler>();
});
```

**Azure Service Bus Configuration (Scaffolding in place)**

```csharp
using Chirp.Application.Common.EventBusOptions;
using Chirp.Infrastructure;

// Note: Azure Service Bus implementation is not yet complete
services.AddChirp(options => 
{
    options.UseTopics = true;
    options.TopicName = "my-topic";
    options.SubscriptionName = "my-subscription";
    options.AutoCreateResources = true;
    
    // Register event handlers
    options.AddConsumer<OrderCreatedEventHandler>();
});
```

### Creating Events

Create event classes that inherit from `IntegrationEvent`:

```csharp
using Chirp.Domain.Common;

public record OrderCreatedEvent(int OrderId, string CustomerName, decimal Total) : IntegrationEvent;
```

### Creating Event Handlers

Create handlers that implement `IChirpIntegrationEventHandler<T>`:

```csharp
using Chirp.Application.Interfaces;

public class OrderCreatedEventHandler : IChirpIntegrationEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        // Process the event
        Console.WriteLine($"Order {@event.OrderId} created for {@event.CustomerName} with total {@event.Total}");
        await Task.CompletedTask;
    }
}
```

### Publishing Events

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

## Advanced Usage

### Using the Event Bus Factory

You can also use the `EventBusFactory` to create an instance of the event bus:

```csharp
IChirpEventBus eventBus = EventBusFactory.Create(
    EventBusType.RabbitMQ,
    serviceProvider,
    configuration,
    "my_service_queue",
    retryCount: 5);
```

### Working with Multiple Message Brokers

Chirp is designed to support multiple message broker implementations. Once additional providers are fully implemented, 
this will be useful in scenarios like:

- Migrating from one messaging system to another
- Creating hybrid systems with different messaging needs
- Publishing to multiple brokers for redundancy
- Consuming messages from different sources

Currently, the library has a fully implemented RabbitMQ provider. Other providers have scaffolding in place but require 
implementation of the core publish/subscribe functionality.

#### Planned Features for Multi-Broker Support

- Message routing based on event type
- Automatic failover between brokers
- Unified configuration for multiple brokers
- Message synchronization between different broker types

## Supported Message Brokers

| Provider              |        Status        | Configuration Section |
|:----------------------|:--------------------:|:----------------------|
| **RabbitMQ**          |  ✅ Fully Implemented | `RMQ`                 |
| **InMemory**          |  ✅ Fully Implemented | `N/A`                 |
| **Kafka**             | 🚧 Scaffolding in Place | `Kafka`               |
| **Redis**             | 🚧 Scaffolding in Place | `Redis`               |
| **Azure Service Bus** | 🚧 Scaffolding in Place | `AzureServiceBus`     |
| **Amazon SQS**        | 🚧 Scaffolding in Place | `AWS:SQS`             |
| **NATS**              | 🚧 Scaffolding in Place | `NATS`                |
| **Google Pub/Sub**    | 🚧 Scaffolding in Place | `GooglePubSub`        |

**Note:** Providers marked with 🚧 have interfaces, option classes, and event bus classes in place, but the 
`PublishAsync` and `SubscribeAsync` methods are not yet implemented and will throw `NotImplementedException`.

## Contributing

Contributions are welcome! Feel free to submit a Pull Request. If you'd like to help implement one of the message 
broker providers, please check the existing scaffolding in the `Infrastructure/EventBus` directory.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
