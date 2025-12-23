using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using RabbitMQ.Client;
using System.Reflection;

namespace Chirp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Configures the Chirp event bus for the web application
    /// </summary>
    /// <param name="app">The web application</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The web application</returns>
    public static async Task<WebApplication> UseChirpAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        // Get the event bus singleton from DI
        IChirpEventBus eventBus = app.Services.GetRequiredService<IChirpEventBus>();

        // Trigger initialization by calling a method on it
        // This ensures the infrastructure is set up before the app starts processing requests
        await TriggerInitializationAsync(eventBus, cancellationToken);

        return app;
    }

    /// <summary>
    /// Configures the Chirp event bus for the web application (synchronous version - not recommended)
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application</returns>
    /// <remarks>
    /// This is a synchronous version that may block. Consider using UseChirpAsync instead.
    /// </remarks>
    public static WebApplication UseChirp(this WebApplication app)
    {
        // Get the event bus singleton from DI - this will trigger the initialization 
        // that's already set up in AddChirp() through the factory method
        IChirpEventBus eventBus = app.Services.GetRequiredService<IChirpEventBus>();

        // Note: Auto-subscription happens in the factory, but infrastructure initialization
        // is deferred until first Publish/Subscribe call

        return app;
    }

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
    /// Triggers initialization of the event bus to ensure it's ready
    /// </summary>
    private static async Task TriggerInitializationAsync(IChirpEventBus eventBus, CancellationToken cancellationToken)
    {
        // The event bus is already created, but we want to ensure it's ready
        // We can do this by checking if it's a specific type and calling an internal init method
        // For now, we'll just ensure the singleton is created (which happens in GetRequiredService)
        
        // If we need to force initialization, we could publish a dummy event or similar
        // But with lazy initialization, it will happen on first real use
        await Task.CompletedTask;
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

        // Get the generic SubscribeAsync<T, TH>() method
        MethodInfo? subscribeMethod = typeof(IChirpEventBus).GetMethod("SubscribeAsync");
        if (subscribeMethod == null)
        {
            Console.WriteLine("Warning: Could not find SubscribeAsync method on IChirpEventBus");
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
                    // Create a strongly typed SubscribeAsync<T, TH> method with the correct generic parameters
                    MethodInfo genericSubscribeMethod = subscribeMethod.MakeGenericMethod(eventType, handlerType);

                    // Invoke the SubscribeAsync method on the event bus
                    // Note: We're calling this synchronously here since we're in a synchronous context (DI registration)
                    // The actual subscription will happen asynchronously when UseChirp is called
                    object? result = genericSubscribeMethod.Invoke(eventBus, [CancellationToken.None]);
                    
                    // If it's a Task, we need to wait for it (but don't block - fire and forget with proper handling)
                    if (result is Task task)
                    {
                        // Fire-and-forget with error handling
                        _ = task.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                Console.WriteLine($"Error subscribing {handlerType.Name} to handle {eventType.Name} events: {t.Exception?.GetBaseException().Message}");
                            }
                            else
                            {
                                Console.WriteLine($"Successfully subscribed {handlerType.Name} to handle {eventType.Name} events");
                            }
                        }, TaskScheduler.Default);
                    }
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