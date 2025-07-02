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

            // Register event bus using options
            return AddEventBus(
                services,
                configuration,
                options.QueueName,
                options.EventBusType,
                options.RetryCount);
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
        var configDescriptor = services.FirstOrDefault(d => 
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
    /// <param name="queueOrTopicName">The queue, topic, or channel name for the messaging service</param>
    /// <param name="eventBusType">The event bus type to use</param>
    /// <param name="retryCount">Number of retries for processing events</param>
    /// <returns>The service collection</returns>
    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
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
            EventBusFactory.Create(
                eventBusType,
                sp,
                configuration,
                queueOrTopicName,
                retryCount));

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
}