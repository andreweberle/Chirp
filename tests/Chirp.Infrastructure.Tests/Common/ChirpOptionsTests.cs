using Chirp.Application.Common;
using Chirp.Application.Common.EventBusOptions;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace Chirp.Infrastructure.Tests.Common;

[TestClass]
public class ChirpOptionsTests
{
    [TestMethod]
    public void RabbitMqChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new RabbitMqChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.RabbitMQ, options.EventBusType);
    }

    [TestMethod]
    public void KafkaChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new KafkaChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.Kafka, options.EventBusType);
    }

    [TestMethod]
    public void AzureServiceBusChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new AzureServiceBusChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.AzureServiceBus, options.EventBusType);
    }

    [TestMethod]
    public void AmazonSqsChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new AmazonSqsChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.AmazonSqs, options.EventBusType);
    }

    [TestMethod]
    public void RedisChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new RedisChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.Redis, options.EventBusType);
    }

    [TestMethod]
    public void GooglePubSubChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new GooglePubSubChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.GooglePubSub, options.EventBusType);
    }

    [TestMethod]
    public void NatsChirpOptions_HasCorrectEventBusType()
    {
        // Arrange & Act
        var options = new NatsChirpOptions();

        // Assert
        Assert.AreEqual(EventBusType.NATS, options.EventBusType);
    }

    [TestMethod]
    public void RabbitMqChirpOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new RabbitMqChirpOptions();

        // Assert
        Assert.AreEqual("chirp_default_queue", options.QueueName);
        Assert.AreEqual(5, options.RetryCount);
        Assert.AreEqual("chirp_event_bus", options.ExchangeName);
        Assert.AreEqual("chirp_dlx_exchange", options.DeadLetterExchangeName);
        Assert.IsTrue(options.AutoCreateExchange);
        Assert.IsTrue(options.AutoCreateQueue);
        Assert.IsTrue(options.QueueDurable);
        Assert.IsTrue(options.PersistentMessages);
    }

    [TestMethod]
    public void KafkaChirpOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new KafkaChirpOptions();

        // Assert
        Assert.AreEqual("chirp_default_queue", options.QueueName);
        Assert.AreEqual("chirp_default_queue", options.TopicName);
        Assert.AreEqual(5, options.RetryCount);
        Assert.AreEqual("chirp_consumer_group", options.ConsumerGroupId);
        Assert.IsTrue(options.AutoCreateTopics);
        Assert.AreEqual(1, options.NumPartitions);
        Assert.AreEqual(1, options.ReplicationFactor);
        Assert.IsNotNull(options.ProducerConfig);
        Assert.IsNotNull(options.ConsumerConfig);
    }

    [TestMethod]
    public void AzureServiceBusChirpOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new AzureServiceBusChirpOptions();

        // Assert
        Assert.AreEqual("chirp_default_queue", options.QueueName);
        Assert.AreEqual(5, options.RetryCount);
        Assert.IsFalse(options.UseTopics);
        Assert.AreEqual("chirp_default_queue", options.TopicName);
        Assert.AreEqual("chirp_subscription", options.SubscriptionName);
        Assert.AreEqual(10, options.MaxConcurrentCalls);
        Assert.IsFalse(options.EnableSessions);
        Assert.AreEqual("$DeadLetterQueue", options.DeadLetterQueuePath);
        Assert.IsTrue(options.AutoCreateResources);
        Assert.AreEqual(TimeSpan.FromDays(14), options.MessageTimeToLive);
    }

    [TestMethod]
    public void AddConsumer_RegistersConsumer()
    {
        // Arrange
        var options = new ChirpOptions(); // Use base class to access internal members

        // Act
        options.AddConsumer<TestHandler>();

        // Use reflection to access internal members
        var consumersProperty = typeof(ChirpOptions).GetProperty("Consumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var consumers = consumersProperty?.GetValue(options) as System.Collections.IList;

        // Assert
        Assert.IsNotNull(consumers);
        Assert.AreEqual(1, consumers.Count);
        // Can't directly check the type, but we know it should have one element
    }

    [TestMethod]
    public void AddConsumer_FluentInterface_ReturnsSameInstance()
    {
        // Arrange
        var options = new ChirpOptions(); // Use base class

        // Act
        var result = options.AddConsumer<TestHandler>();

        // Assert
        Assert.AreSame(options, result);
    }

    [TestMethod]
    public void AddConsumer_MultipleCalls_RegistersAllConsumers()
    {
        // Arrange
        var options = new ChirpOptions(); // Use base class

        // Act
        options
            .AddConsumer<TestHandler>()
            .AddConsumer<AnotherTestHandler>();

        // Use reflection to access internal members
        var consumersProperty = typeof(ChirpOptions).GetProperty("Consumers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var consumers = consumersProperty?.GetValue(options) as System.Collections.IList;

        // Assert
        Assert.IsNotNull(consumers);
        Assert.AreEqual(2, consumers.Count);
    }

    // Test classes for consumer registration tests
    private class TestHandler : IIChirpIntegrationEventHandler
    {
    }

    private class AnotherTestHandler : IIChirpIntegrationEventHandler
    {
    }
}