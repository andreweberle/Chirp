using Chirp.Application.Interfaces;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Chirp.Infrastructure.EventBus.Kafka;
using Chirp.Infrastructure.EventBus.AzureServiceBus;
using Chirp.Infrastructure.EventBus.AmazonSQS;
using Chirp.Infrastructure.EventBus.Redis;
using Chirp.Infrastructure.EventBus.GooglePubSub;
using Chirp.Infrastructure.EventBus.NATS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using System;
using System.Linq;
using Chirp.Domain.Common;
using System.Reflection;

namespace Chirp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adds Chirp messaging services to the service collection with a fluent configuration API
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<ChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration != null)
        {
            ChirpOptions options = new ChirpOptions();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = AddEventBus(
                services,
                configuration,
                options,
                options.QueueName,
                options.EventBusType,
                options.RetryCount);

            return services;
        }

        // Create and configure options
        throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Adds Chirp messaging services to the service collection with a fluent configuration API
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<ChirpOptions> configureOptions)
    {
        // Get IConfiguration from service descriptors directly
        ServiceDescriptor? configDescriptor = services.FirstOrDefault(d => 
            d.ServiceType == typeof(IConfiguration));
            
        if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
        {
            return AddChirp(services, configureOptions, configuration);
        }
        
        throw new InvalidOperationException(
            "IConfiguration is not registered in the service collection. " +
            "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
    }

    /// <summary>
    /// Adds event bus services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="options">The chirp options</param>
    /// <param name="queueOrTopicName">The queue, topic, or channel name for the messaging service</param>
    /// <param name="eventBusType">The event bus type to use</param>
    /// <param name="retryCount">Number of retries for processing events</param>
    /// <returns>The service collection</returns>
    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        ChirpOptions options,
        string queueOrTopicName,
        EventBusType eventBusType = EventBusType.RabbitMQ,
        int retryCount = 5)
    {
        // Register necessary connections based on event bus type
        switch (eventBusType)
        {
            case EventBusType.RabbitMQ:
                services.AddRabbitMqConnection(configuration);
                break;

            case EventBusType.Kafka:
                // Uncomment when implementing Kafka
                // services.AddKafkaConnection(configuration);
                throw new NotImplementedException("Kafka event bus is not implemented yet");

            case EventBusType.AzureServiceBus:
                // Uncomment when implementing Azure Service Bus
                // services.AddAzureServiceBusConnection(configuration);
                throw new NotImplementedException("Azure Service Bus is not implemented yet");

            case EventBusType.AmazonSqs:
                // Uncomment when implementing Amazon SQS
                // services.AddAmazonSQSConnection(configuration);
                throw new NotImplementedException("Amazon SQS is not implemented yet");

            case EventBusType.Redis:
                // Uncomment when implementing Redis
                // services.AddRedisConnection(configuration);
                throw new NotImplementedException("Redis Pub/Sub is not implemented yet");

            case EventBusType.GooglePubSub:
                // Uncomment when implementing Google Cloud Pub/Sub
                // services.AddGooglePubSubConnection(configuration);
                throw new NotImplementedException("Google Cloud Pub/Sub is not implemented yet");

            case EventBusType.NATS:
                // Uncomment when implementing NATS
                // services.AddNATSConnection(configuration);
                throw new NotImplementedException("NATS is not implemented yet");

            default:
                throw new ArgumentException($"Unsupported event bus type: {eventBusType}");
        }

        // Register the event bus using the factory
        services.AddSingleton<IEventBus>(sp =>
        {
            var eventBus = EventBusFactory.Create(
                eventBusType,
                sp,
                configuration,
                queueOrTopicName,
                retryCount);
            
            // Auto-subscribe all registered handlers
            AutoSubscribeEventHandlers(eventBus, options, sp);
            
            return eventBus;
        });

        return services;
    }

    /// <summary>
    /// Registers the RabbitMQ connection
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    private static IServiceCollection AddRabbitMqConnection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            string host = configuration["RMQ:Host"] ?? throw new ArgumentNullException("RMQ:Host configuration is missing");
            string username = configuration["RMQ:Username"] ?? throw new ArgumentNullException("RMQ:Username configuration is missing");
            string password = configuration["RMQ:Password"] ?? throw new ArgumentNullException("RMQ:Password configuration is missing");

            IConnectionFactory connectionFactory = Domain.Common.ConnectionFactory.CreateConnectionFactory(host, username, password);
            return new RabbitMqConnection(connectionFactory);
        });

        return services;
    }

    /// <summary>
    /// Registers event consumers as transient services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The chirp options containing consumers to register</param>
    private static void RegisterConsumers(IServiceCollection services, ChirpOptions options)
    {
        // Early return if no consumers are registered
        if (options.Consumers.Count == 0) return;

        // Register all consumer handler types as transient services
        foreach (ConsumerRegistration consumer in options.Consumers)
        {
            // Register the handler as itself
            services.AddTransient(consumer.HandlerType);

            // Find all interface implementations of IIntegrationEventHandler<T>
            foreach (var implementedInterface in consumer.HandlerType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType)
                    continue;

                if (implementedInterface.GetGenericTypeDefinition() != typeof(IIntegrationEventHandler<>))
                    continue;

                // Get the event type from the generic argument
                Type eventType = implementedInterface.GetGenericArguments()[0];
                
                // Register the handler as IIntegrationEventHandler<TEvent>
                Type handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                services.AddTransient(handlerInterfaceType, consumer.HandlerType);
            }
        }
    }
    
    /// <summary>
    /// Automatically subscribes all registered handlers to the event bus
    /// </summary>
    /// <param name="eventBus">The event bus</param>
    /// <param name="options">The chirp options</param>
    /// <param name="serviceProvider">The service provider</param>
    private static void AutoSubscribeEventHandlers(IEventBus eventBus, ChirpOptions options, IServiceProvider serviceProvider)
    {
        if (eventBus == null || options.Consumers.Count == 0)
            return;

        // Get the generic Subscribe<T, TH>() method
        var subscribeMethod = typeof(IEventBus).GetMethod("Subscribe");
        if (subscribeMethod == null)
        {
            Console.WriteLine("Warning: Could not find Subscribe method on IEventBus");
            return;
        }

        foreach (var consumer in options.Consumers)
        {
            Type handlerType = consumer.HandlerType;

            // Find all IIntegrationEventHandler<T> interfaces implemented by the handler
            var eventHandlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .ToList();

            foreach (var handlerInterface in eventHandlerInterfaces)
            {
                // Get the event type from the generic argument of IIntegrationEventHandler<T>
                Type eventType = handlerInterface.GetGenericArguments()[0];

                try
                {
                    // Create a strongly typed Subscribe<T, TH> method with the correct generic parameters
                    var genericSubscribeMethod = subscribeMethod.MakeGenericMethod(eventType, handlerType);
                    
                    // Invoke the Subscribe method on the event bus
                    genericSubscribeMethod.Invoke(eventBus, Array.Empty<object>());
                    
                    // Log success for debugging (can be removed in production or replaced with proper logging)
                    Console.WriteLine($"Successfully subscribed {handlerType.Name} to handle {eventType.Name} events");
                }
                catch (Exception ex)
                {
                    // Log error for debugging (can be removed in production or replaced with proper logging)
                    Console.WriteLine($"Error subscribing {handlerType.Name} to handle {eventType.Name} events: {ex.Message}");
                    
                    // Don't throw - continue with other handlers
                }
            }
        }
    }
}