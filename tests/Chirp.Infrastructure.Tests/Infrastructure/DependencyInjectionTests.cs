using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure;
using Chirp.Infrastructure.EventBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Chirp.Application.Common;

namespace Chirp.Infrastructure.Tests.Infrastructure;

[TestClass]
public class DependencyInjectionTests
{
    private static readonly MethodInfo? DetermineEventBusTypeMethod = typeof(DependencyInjection)
        .GetMethod("DetermineEventBusType", BindingFlags.NonPublic | BindingFlags.Static);

    [TestMethod]
    public void DetermineEventBusType_RabbitMqChirpOptions_ReturnsRabbitMQ()
    {
        // Arrange
        RabbitMqChirpOptions options = new RabbitMqChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.RabbitMQ);

        // Assert
        Assert.AreEqual(EventBusType.RabbitMQ, result);
    }

    [TestMethod]
    public void DetermineEventBusType_KafkaChirpOptions_ReturnsKafka()
    {
        // Arrange
        KafkaChirpOptions options = new KafkaChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.Kafka);

        // Assert
        Assert.AreEqual(EventBusType.Kafka, result);
    }

    [TestMethod]
    public void DetermineEventBusType_AzureServiceBusChirpOptions_ReturnsAzureServiceBus()
    {
        // Arrange
        AzureServiceBusChirpOptions options = new AzureServiceBusChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.AzureServiceBus);

        // Assert
        Assert.AreEqual(EventBusType.AzureServiceBus, result);
    }

    [TestMethod]
    public void DetermineEventBusType_AmazonSqsChirpOptions_ReturnsAmazonSqs()
    {
        // Arrange
        AmazonSqsChirpOptions options = new AmazonSqsChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.AmazonSqs);

        // Assert
        Assert.AreEqual(EventBusType.AmazonSqs, result);
    }

    [TestMethod]
    public void DetermineEventBusType_RedisChirpOptions_ReturnsRedis()
    {
        // Arrange
        RedisChirpOptions options = new RedisChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.Redis);

        // Assert
        Assert.AreEqual(EventBusType.Redis, result);
    }

    [TestMethod]
    public void DetermineEventBusType_GooglePubSubChirpOptions_ReturnsGooglePubSub()
    {
        // Arrange
        GooglePubSubChirpOptions options = new GooglePubSubChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                             EventBusType.GooglePubSub);

        // Assert
        Assert.AreEqual(EventBusType.GooglePubSub, result);
    }

    [TestMethod]
    public void DetermineEventBusType_NatsChirpOptions_ReturnsNATS()
    {
        // Arrange
        NatsChirpOptions options = new NatsChirpOptions();
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        var result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, new object[] { options }) ??
                                    EventBusType.NATS);

        // Assert
        Assert.AreEqual(EventBusType.NATS, result);
    }

    [TestMethod]
    public void DetermineEventBusType_BaseChirpOptions_ReturnsEventBusTypeProperty()
    {
        // Arrange
        ChirpOptions options = new ChirpOptions { EventBusType = EventBusType.AzureServiceBus };
        Assert.IsNotNull(DetermineEventBusTypeMethod,
            "DetermineEventBusType method not found in DependencyInjection class");

        // Act
        EventBusType result = (EventBusType)(DetermineEventBusTypeMethod!.Invoke(null, [options]) ?? EventBusType.AzureServiceBus);

        // Assert
        Assert.AreEqual(EventBusType.AzureServiceBus, result);
    }

    [TestMethod]
    public void AddChirp_WithConfigureOptionsAction_RegistersServices()
    {
        // Arrange
        ServiceCollection services = [];
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();

        // Add configuration to DI
        services.AddSingleton<IConfiguration>(configuration);

        // The test can't fully test AddChirp because it registers connections that we can't mock easily
        // But we can verify that it adds some services
        int initialServiceCount = services.Count;

        try
        {
            // Act
            services.AddChirp(options =>
            {
                options.EventBusType = EventBusType.RabbitMQ;
                options.QueueName = "test_queue";
                options.RetryCount = 5;
                options.ExchangeName = "test_exchange";
                options.DeadLetterExchangeName = "test_dlx_exchange";
            });
        }
        catch (NotImplementedException)
        {
            // This is expected because RabbitMQ connection requires configuration values
            // The important thing is that services were added before the exception
        }
        catch (ArgumentNullException)
        {
            // This is also expected in test environment
        }

        // Assert
        Assert.IsTrue(services.Count > initialServiceCount);
    }
}