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
        IChirpEventBusSubscriptionsManager eventBusSubscriptionsManager,
        string queueUrl,
        string deadLetterQueueUrl,
        int retryMax = 5)
        : base(retryMax, serviceProvider, eventBusSubscriptionsManager)
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
    public override Task<bool> PublishAsync(IntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Subscribes to an event with the specified handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <typeparam name="TH">The event handler type</typeparam>
    public override Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}