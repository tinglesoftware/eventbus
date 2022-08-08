using Microsoft.Extensions.Options;
using Tingle.EventBus.Transports.Amazon.Kinesis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Amazon Kinesis.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Add Amazon Kinesis as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonKinesisTransport(this EventBusBuilder builder, Action<AmazonKinesisTransportOptions> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        // Configure the options for Amazon Kinesis
        services.Configure(configure);
        services.ConfigureOptions<AmazonKinesisPostConfigureOptions>();

        // Register the transport
        builder.AddTransport<AmazonKinesisTransport, AmazonKinesisTransportOptions>();

        return builder;
    }
}
