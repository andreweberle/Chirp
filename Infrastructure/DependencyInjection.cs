﻿using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.AmazonSQS;
using Chirp.Infrastructure.EventBus.AzureServiceBus;
using Chirp.Infrastructure.EventBus.GooglePubSub;
using Chirp.Infrastructure.EventBus.Kafka;
using Chirp.Infrastructure.EventBus.NATS;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Chirp.Infrastructure.EventBus.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Reflection;

namespace Chirp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Configures the Chirp event bus for the application
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder</returns>
    public static IApplicationBuilder UseChirp(this IApplicationBuilder app)
    {
        // Get the event bus singleton from DI - this will trigger the initialization 
        // that's already set up in AddChirp() through the factory method
        IChirpEventBus eventBus = app.ApplicationServices.GetRequiredService<IChirpEventBus>();

        // No need to re-subscribe handlers as that's already done in AddChirp
        // when the singleton is created

        return app;
    }

    /// <summary>
    /// Configures the Chirp event bus for the host
    /// </summary>
    /// <param name="host">The host</param>
    /// <returns>The host</returns>
    public static IHost UseChirp(this IHost host)
    {
        // Get the event bus singleton from DI - this will trigger the initialization 
        // that's already set up in AddChirp() through the factory method
        IChirpEventBus eventBus = host.Services.GetRequiredService<IChirpEventBus>();

        // No need to re-subscribe handlers as that's already done in AddChirp
        // when the singleton is created

        return host;
    }

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
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        ChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with RabbitMQ implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure RabbitMQ options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<RabbitMqChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        RabbitMqChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with Kafka implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Kafka options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<KafkaChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        KafkaChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with Azure Service Bus implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Azure Service Bus options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<AzureServiceBusChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        AzureServiceBusChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with Amazon SQS implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Amazon SQS options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<AmazonSqsChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        AmazonSqsChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with Redis implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Redis options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<RedisChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        RedisChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with Google Pub/Sub implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Google Pub/Sub options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<GooglePubSubChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        GooglePubSubChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
    }

    /// <summary>
    /// Adds Chirp messaging services with NATS implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure NATS options</param>
    /// <param name="configuration">The configuration (required)</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<NatsChirpOptions> configureOptions,
        IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        NatsChirpOptions options = new();
        configureOptions(options);

        // Register consumers first so they're available when the event bus is created
        RegisterConsumers(services, options);

        // Register event bus using options
        services = AddEventBus(
            services,
            configuration,
            options);

        return services;
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
    /// Adds RabbitMQ Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure RabbitMQ options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<RabbitMqChirpOptions> configureOptions)
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
    /// Adds Kafka Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Kafka options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<KafkaChirpOptions> configureOptions)
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
    /// Adds Azure Service Bus Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Azure Service Bus options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<AzureServiceBusChirpOptions> configureOptions)
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
    /// Adds Amazon SQS Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Amazon SQS options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<AmazonSqsChirpOptions> configureOptions)
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
    /// Adds Redis Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Redis options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<RedisChirpOptions> configureOptions)
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
    /// Adds Google Pub/Sub Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure Google Pub/Sub options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<GooglePubSubChirpOptions> configureOptions)
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
    /// Adds NATS Chirp messaging services with type-specific configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure NATS options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddChirp(
        this IServiceCollection services,
        Action<NatsChirpOptions> configureOptions)
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
    /// Determines the event bus type from the options object
    /// </summary>
    /// <param name="options">The chirp options</param>
    /// <returns>The event bus type</returns>
    private static EventBusType DetermineEventBusType(ChirpOptions options)
    {
        return options switch
        {
            RabbitMqChirpOptions => EventBusType.RabbitMQ,
            KafkaChirpOptions => EventBusType.Kafka,
            AzureServiceBusChirpOptions => EventBusType.AzureServiceBus,
            AmazonSqsChirpOptions => EventBusType.AmazonSqs,
            RedisChirpOptions => EventBusType.Redis,
            GooglePubSubChirpOptions => EventBusType.GooglePubSub,
            NatsChirpOptions => EventBusType.NATS,
            _ => options.EventBusType
        };
    }

    /// <summary>
    /// Adds event bus services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="options">The chirp options</param>
    /// <returns>The service collection</returns>
    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        ChirpOptions options)
    {
        // Determine the event bus type from options
        EventBusType eventBusType = DetermineEventBusType(options);

        // Register necessary connections based on event bus type
        switch (eventBusType)
        {
            case EventBusType.RabbitMQ:
                services.AddRabbitMqConnection(configuration, options as RabbitMqChirpOptions);
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
        services.AddSingleton<IChirpEventBus>(sp =>
        {
            // Create event bus based on options type, using the new overload
            IChirpEventBus eventBus = EventBusFactory.Create(
                options,
                sp,
                configuration);

            // Auto-subscribe all registered handlers
            AutoSubscribeEventHandlers(eventBus, options, sp);
            return eventBus;
        });

        return services;
    }

    /// <summary>
    /// Creates an appropriate event bus instance based on the options type
    /// </summary>
    private static IChirpEventBus CreateEventBus(
        ChirpOptions options,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        // For base ChirpOptions, use the EventBusType property and the EventBusFactory
        EventBusType eventBusType = DetermineEventBusType(options);

        // Attempt to create a specific event bus type based on options
        return EventBusFactory.Create(
            options,
            serviceProvider,
            configuration);
    }

    /// <summary>
    /// Registers the RabbitMQ connection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="options">Optional RabbitMQ options that may contain connection details</param>
    /// <exception cref="ArgumentNullException"></exception>
    private static IServiceCollection AddRabbitMqConnection(
        this IServiceCollection services,
        IConfiguration configuration,
        RabbitMqChirpOptions? options = null)
    {
        services.AddSingleton<IChirpRabbitMqConnection>(sp =>
        {
            // Use options values if provided, otherwise fall back to configuration
            string host = options?.Host ?? configuration["RMQ:Host"] ?? throw new ArgumentNullException("RMQ:Host configuration is missing");
            string username = options?.Username ?? configuration["RMQ:Username"] ?? throw new ArgumentNullException("RMQ:Username configuration is missing");
            string password = options?.Password ?? configuration["RMQ:Password"] ?? throw new ArgumentNullException("RMQ:Password configuration is missing");

            IConnectionFactory connectionFactory = Domain.Common.ConnectionFactory.CreateConnectionFactory(host, username, password);
            return new ChirpRabbitMqConnection(connectionFactory);
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
            foreach (Type implementedInterface in consumer.HandlerType.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType)
                    continue;

                if (implementedInterface.GetGenericTypeDefinition() != typeof(IChirpIntegrationEventHandler<>))
                    continue;

                // Get the event type from the generic argument
                Type eventType = implementedInterface.GetGenericArguments()[0];

                // Register the handler as IIntegrationEventHandler<TEvent>
                Type handlerInterfaceType = typeof(IChirpIntegrationEventHandler<>).MakeGenericType(eventType);
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
    private static void AutoSubscribeEventHandlers(IChirpEventBus eventBus, ChirpOptions options,
        IServiceProvider serviceProvider)
    {
        if (options.Consumers.Count == 0)
            return;

        // Get the generic Subscribe<T, TH>() method
        MethodInfo? subscribeMethod = typeof(IChirpEventBus).GetMethod("Subscribe");
        if (subscribeMethod == null)
        {
            Console.WriteLine("Warning: Could not find Subscribe method on IEventBus");
            return;
        }

        foreach (ConsumerRegistration consumer in options.Consumers)
        {
            Type handlerType = consumer.HandlerType;

            // Find all IIntegrationEventHandler<T> interfaces implemented by the handler
            List<Type> eventHandlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IChirpIntegrationEventHandler<>))
                .ToList();

            foreach (Type eventType in eventHandlerInterfaces.Select(handlerInterface =>
                         handlerInterface.GetGenericArguments()[0]))
            {
                try
                {
                    // Create a strongly typed Subscribe<T, TH> method with the correct generic parameters
                    MethodInfo genericSubscribeMethod = subscribeMethod.MakeGenericMethod(eventType, handlerType);

                    // Invoke the Subscribe method on the event bus
                    genericSubscribeMethod.Invoke(eventBus, []);

                    // Log success for debugging (can be removed in production or replaced with proper logging)
                    Console.WriteLine($"Successfully subscribed {handlerType.Name} to handle {eventType.Name} events");
                }
                catch (Exception ex)
                {
                    // Log error for debugging (can be removed in production or replaced with proper logging)
                    Console.WriteLine(
                        $"Error subscribing {handlerType.Name} to handle {eventType.Name} events: {ex.Message}");

                    // Don't throw - continue with other handlers
                }
            }
        }
    }
}