using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring Amazon SQS based event bus.
/// </summary>
public class AmazonSqsTransportOptions : AmazonTransportOptions
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Queue;

    /// <summary>
    /// Configuration for SQS
    /// </summary>
    public AmazonSQSConfig? SqsConfig { get; set; }

    /// <summary>
    /// Configuration for SNS
    /// </summary>
    public AmazonSimpleNotificationServiceConfig? SnsConfig { get; set; }

    /// <summary>
    /// A setup function for setting up settings for a topic.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, CreateTopicRequest>? SetupCreateTopicRequest { get; set; }

    /// <summary>
    /// A setup function for setting up settings for a queue.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, EventConsumerRegistration?, CreateQueueRequest>? SetupCreateQueueRequest { get; set; }
}
