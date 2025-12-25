using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using RabbitMQ.Client;
using System.Reflection;
using System.Threading.Channels;
using Chirp.Application.Common;
using Chirp.Infrastructure.EventBus.InMemory;

namespace Chirp.Infrastructure;

public static class DependencyInjection
{
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

    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        public IServiceCollection AddChirp(Action<InMemoryOptions> configureOptions)
        {
            InMemoryOptions options = new InMemoryOptions();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(null, options);
            
            // Return the service collection
            return services;
        }
        
        
        /// <summary>
        /// Adds Chirp messaging services to the service collection with a fluent configuration API
        /// </summary>
        /// <param name="configureOptions">Action to configure options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<ChirpOptions> configureOptions, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with RabbitMQ implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure RabbitMQ options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<RabbitMqChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            RabbitMqChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);
        
            // Return the service collection
            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with Kafka implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure Kafka options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<KafkaChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            KafkaChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with Azure Service Bus implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure Azure Service Bus options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<AzureServiceBusChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            AzureServiceBusChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with Amazon SQS implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure Amazon SQS options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<AmazonSqsChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            AmazonSqsChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with Redis implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure Redis options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<RedisChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            RedisChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with Google Pub/Sub implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure Google Pub/Sub options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<GooglePubSubChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            GooglePubSubChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services with NATS implementation
        /// </summary>
        /// <param name="configureOptions">Action to configure NATS options</param>
        /// <param name="configuration">The configuration (required)</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<NatsChirpOptions> configureOptions,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            NatsChirpOptions options = new();
            configureOptions(options);

            // Register consumers first so they're available when the event bus is created
            RegisterConsumers(services, options);

            // Register event bus using options
            services = services.AddEventBus(configuration, options);

            return services;
        }

        /// <summary>
        /// Adds Chirp messaging services to the service collection with a fluent configuration API
        /// </summary>
        /// <param name="configureOptions">Action to configure options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<ChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds RabbitMQ Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure RabbitMQ options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<RabbitMqChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds Kafka Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure Kafka options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<KafkaChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds Azure Service Bus Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure Azure Service Bus options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<AzureServiceBusChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds Amazon SQS Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure Amazon SQS options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<AmazonSqsChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds Redis Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure Redis options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<RedisChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds Google Pub/Sub Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure Google Pub/Sub options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<GooglePubSubChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }

        /// <summary>
        /// Adds NATS Chirp messaging services with type-specific configuration
        /// </summary>
        /// <param name="configureOptions">Action to configure NATS options</param>
        /// <returns>The service collection</returns>
        public IServiceCollection AddChirp(Action<NatsChirpOptions> configureOptions)
        {
            // Get IConfiguration from service descriptors directly
            ServiceDescriptor? configDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IConfiguration));

            if (configDescriptor?.ImplementationInstance is IConfiguration configuration)
            {
                return services.AddChirp(configureOptions, configuration);
            }

            throw new InvalidOperationException(
                "IConfiguration is not registered in the service collection. " +
                "Please register IConfiguration before calling AddChirp or use the overload that accepts IConfiguration.");
        }
    }

    /// <summary>
    /// Determines the event bus type from the options object
    /// </summary>
    /// <param name="options">The chirp options</param>
    /// <returns>The event bus type</returns>
    private static EventBusType DetermineEventBusType(ChirpOptions options) => options switch
    {
        RabbitMqChirpOptions => EventBusType.RabbitMQ,
        KafkaChirpOptions => EventBusType.Kafka,
        AzureServiceBusChirpOptions => EventBusType.AzureServiceBus,
        AmazonSqsChirpOptions => EventBusType.AmazonSqs,
        RedisChirpOptions => EventBusType.Redis,
        GooglePubSubChirpOptions => EventBusType.GooglePubSub,
        NatsChirpOptions => EventBusType.NATS,
        InMemoryOptions => EventBusType.InMemory,
        _ => options.EventBusType
    };

    /// <summary>
    /// Adds event bus services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="options">The chirp options</param>
    /// <returns>The service collection</returns>
    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration? configuration,
        ChirpOptions options)
    {
        // Determine the event bus type from options
        EventBusType eventBusType = DetermineEventBusType(options);

        // Register necessary connections based on event bus type
        switch (eventBusType)
        {
            case EventBusType.InMemory:
                services.AddInMemoryEventBusConnection(options as InMemoryOptions);
                break;
            
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
            
            // Return the event bus instance
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

    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInMemoryEventBusConnection(InMemoryOptions? options = null)
        {
            // TODO: Check if the user wants a persistent in-memory store (e.g., using LiteDB or similar)
            
            // Create a channel for publishing events.
            services.AddSingleton<Channel<IntegrationEvent>>(
                _ => Channel.CreateUnbounded<IntegrationEvent>(new UnboundedChannelOptions()
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                }));
            
            // Create the in-memory event bus connection
            services.AddSingleton<IChirpInMemoryDeadLetterQueue, InMemoryDeadLetterQueue>();
            
            // Register the background processor, this will be used to process events.
            services.AddHostedService<ChirpInMemoryProcessor>();
            
            // Return the service collection
            return services;
        }

        /// <summary>
        /// Registers the RabbitMQ connection
        /// </summary>
        /// <param name="configuration">The configuration</param>
        /// <param name="options">Optional RabbitMQ options that may contain connection details</param>
        /// <exception cref="ArgumentNullException"></exception>
        private IServiceCollection AddRabbitMqConnection(IConfiguration configuration,
            RabbitMqChirpOptions? options = null)
        {
            services.AddSingleton<IChirpRabbitMqConnection>(sp =>
            {
                string host = options?.Host ?? configuration.GetValue<string>("RMQ:Host") ?? throw new ArgumentNullException("RMQ:Host configuration is missing");
                int port = options.Port ?? configuration.GetValue<int>("RMQ:Port", 5672);
                string username = options?.Username ?? configuration.GetValue<string>("RMQ:Username") ?? throw new ArgumentNullException("RMQ:Username configuration is missing");
                string password = options?.Password ?? configuration.GetValue<string>("RMQ:Password") ?? throw new ArgumentNullException("RMQ:Password configuration is missing");
                bool automaticRecoveryEnabled = options?.AutomaticRecoveryEnabled ?? configuration.GetValue<bool>("RMQ:AutomaticRecoveryEnabled", true);
                bool topologyRecoveryEnabled = options?.TopologyRecoveryEnabled ?? configuration.GetValue<bool>("RMQ:TopologyRecoveryEnabled", true);
                TimeSpan networkRecoveryInterval = options?.NetworkRecoveryInterval ?? configuration.GetValue<TimeSpan>("RMQ:NetworkRecoveryInterval", TimeSpan.FromSeconds(5));
                TimeSpan requestedHeartbeat = options?.RequestedHeartbeat ?? configuration.GetValue<TimeSpan>("RMQ:RequestedHeartbeat", TimeSpan.FromSeconds(60));
            
                // Create a RabbitMQ connection using the connection factory
                IConnectionFactory connectionFactory = Domain.Common.ConnectionFactory.CreateConnectionFactory
                (
                    // Connection details
                    host, username, password, port, automaticRecoveryEnabled, topologyRecoveryEnabled, networkRecoveryInterval, requestedHeartbeat
                );
            
                // Create a RabbitMQ connection based on the factory
                return new ChirpRabbitMqConnection(connectionFactory);
            });

            return services;
        }
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
                // Skip if the interface is not generic
                if (!implementedInterface.IsGenericType)
                    continue;

                // Check if the interface is IIntegrationEventHandler<T>
                if (implementedInterface.GetGenericTypeDefinition() != typeof(IChirpIntegrationEventHandler<>))
                    continue;

                // Get the event type from the generic argument
                Type eventType = implementedInterface.GetGenericArguments()[0];

                // Register the handler as IIntegrationEventHandler<TEvent>
                Type handlerInterfaceType = typeof(IChirpIntegrationEventHandler<>).MakeGenericType(eventType);
                
                // Add the handler as a transient service so it can be resolved by the event bus
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
    private static void AutoSubscribeEventHandlers(IChirpEventBus eventBus, ChirpOptions options, IServiceProvider serviceProvider)
    {
        // Check if there are any consumers to subscribe.
        if (options.Consumers.Count == 0)
        {
            // Log a warning if there are no consumers to subscribe to.
            Console.WriteLine("Warning: No consumers registered. Skipping auto-subscription.");
            
            return;
        }
        
        // Check if we need to skip auto-subscription.
        if (!options.AutoSubscribeConsumers)
        {
            // Log a warning if auto-subscription is disabled.
            Console.WriteLine("Warning: Auto-subscription is disabled. Skipping auto-subscription.");

            return;
        }

        // Get the generic SubscribeAsync<T, TH>() method
        MethodInfo? subscribeAsyncMethod = typeof(IChirpEventBus).GetMethod("SubscribeAsync");

        // Sanity check
        if (subscribeAsyncMethod == null)
        {
            Console.WriteLine("Warning: Could not find SubscribeAsync method on IChirpEventBus");
            return;
        }

        // Use a HashSet to track processed (EventType, HandlerType) pairs to prevent duplicates
        // in case the same handler implements multiple interfaces or is registered multiple times
        HashSet<(Type, Type)> processedSubscriptions = [];

        // Iterate over all registered consumers
        foreach (ConsumerRegistration consumer in options.Consumers)
        {
            // Get the handler type
            Type handlerType = consumer.HandlerType;

            // Find all IIntegrationEventHandler<T> interfaces implemented by the handler
            List<Type> eventHandlerInterfaces = 
            [
                .. handlerType.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IChirpIntegrationEventHandler<>))
            ];

            // Subscribe the handler for each event type it handles
            foreach (Type eventType in eventHandlerInterfaces.Select(handlerInterface => handlerInterface.GetGenericArguments()[0]))
            {
                // Skip if we've already processed this combination
                if (!processedSubscriptions.Add((eventType, handlerType)))
                {
                    continue;
                }

                try
                {
                    // Bind generic type arguments to SubscribeAsync<TEvent, THandler>
                    MethodInfo closedSubscribeMethod = subscribeAsyncMethod.MakeGenericMethod(eventType, handlerType);

                    // Invoke SubscribeAsync on the event bus
                    object? returnValue = closedSubscribeMethod.Invoke(eventBus, [CancellationToken.None]);

                    // Await the subscription to ensure sequential registration
                    if (returnValue is Task subscriptionTask)
                    {
                        subscriptionTask.GetAwaiter().GetResult();
                        Console.WriteLine(
                            $"Successfully subscribed {handlerType.Name} to handle {eventType.Name} events");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Warning: SubscribeAsync did not return a Task for {handlerType.Name} handling {eventType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    // Log error for debugging (can be removed in production or replaced with proper logging)
                    Console.WriteLine(
                        $"Error subscribing {handlerType.Name} to handle {eventType.Name} events: {ex.Message}");

                    // Don't throw - continue with other handlers
                }
                finally
                {
                    Console.WriteLine($"--------------");
                }
            }
        }
    }
}