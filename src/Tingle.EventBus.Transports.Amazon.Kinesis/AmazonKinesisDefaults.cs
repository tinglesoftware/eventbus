using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Amazon.Kinesis;

/// <summary>Defaults for <see cref="AmazonKinesisTransportOptions"/>.</summary>
public static class AmazonKinesisDefaults
{
    /// <summary>Default name for <see cref="AmazonKinesisTransportOptions"/>.</summary>
    public const string Name = "amazon-kinesis";
}
