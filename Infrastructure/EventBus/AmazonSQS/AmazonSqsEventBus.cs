using System.Text.Json;
using Chirp.Application.Interfaces;
using Chirp.Domain.Common;
using Chirp.Infrastructure.EventBus;
using Chirp.Infrastructure.EventBus.Common;
using Chirp.Infrastructure.EventBus.AmazonSQS;
using Microsoft.Extensions.DependencyInjection;

namespace Chirp.Infrastructure.EventBus.AmazonSQS;

/// <summary>
/// Amazon SQS implementation of the event bus.
/// </summary>
public class AmazonSqsEventBus : EventBusBase
{
    private readonly IAmazonSQSConnection _sqsConnection;
    private readonly int _retryMax;
    private readonly string _queueUrl;
    private readonly string _deadLetterQueueUrl;

    // Serialize/deserialize options
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of AmazonSQSEventBus
    /// </summary>
    /// <param name="sqsConnection">The Amazon SQS connection</param>
    /// <param name="serviceProvider">The service provider for resolving handlers</param>
    /// <param name="eventBusSubscriptionsManager">The subscription manager</param>
    /// <param name="queueUrl">The queue URL</param>
    /// <param name="deadLetterQueueUrl">The dead letter queue URL</param>
    /// <param name="retryMax">Maximum number of retries</param>
    public AmazonSqsEventBus(
        IAmazonSQSConnection sqsConnection,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string queueUrl,
        string deadLetterQueueUrl,
        int retryMax = 5)
        : base(eventBusSubscriptionsManager, serviceProvider)
    {
        _sqsConnection = sqsConnection ?? throw new ArgumentNullException(nameof(sqsConnection));
        _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
        _deadLetterQueueUrl = deadLetterQueueUrl ?? throw new ArgumentNullException(nameof(deadLetterQueueUrl));
        _retryMax = retryMax;
    }

    /// <summary>
    /// Publishes an event to Amazon SQS
    /// </summary>
    /// <param name="event">The event to publish</param>
    public override void Publish(IntegrationEvent @event)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override void Subscribe<T, TH>()
    {
        throw new NotImplementedException();
    }

    private async Task ProcessMessageAsync(string message, IDictionary<string, string> attributes)
    {
        throw new NotImplementedException();
    }

    private void StartMessageProcessing()
    {
        throw new NotImplementedException();
    }
}