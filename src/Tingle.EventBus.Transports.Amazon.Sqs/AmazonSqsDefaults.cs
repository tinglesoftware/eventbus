using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Amazon.Sqs;

/// <summary>Defaults for <see cref="AmazonSqsTransportOptions"/>.</summary>
public static class AmazonSqsDefaults
{
    /// <summary>Default name for <see cref="AmazonSqsTransportOptions"/>.</summary>
    public const string Name = "amazon-sqs";
}
