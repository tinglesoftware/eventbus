using Tingle.EventBus;
using Tingle.EventBus.Transports.Amazon.Kinesis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Amazon Kinesis.
/// </summary>
public static class AmazonKinesisEventBusBuilderExtensions
{
    /// <summary>Add Amazon Kinesis transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonKinesisTransport(this EventBusBuilder builder, Action<AmazonKinesisTransportOptions>? configure = null)
        => builder.AddAmazonKinesisTransport(TransportNames.AmazonKinesis, configure);

    /// <summary>Add Amazon Kinesis transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonKinesisTransport(this EventBusBuilder builder, string name, Action<AmazonKinesisTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<AmazonKinesisConfigureOptions>();
        return builder.AddTransport<AmazonKinesisTransportOptions, AmazonKinesisTransport>(name, configure);
    }
}
