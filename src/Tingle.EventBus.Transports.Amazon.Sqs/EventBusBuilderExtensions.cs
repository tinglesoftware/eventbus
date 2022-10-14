using Tingle.EventBus;
using Tingle.EventBus.Transports.Amazon.Sqs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Amazon SQS.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>Add Amazon SQS transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonSqsTransport(this EventBusBuilder builder, Action<AmazonSqsTransportOptions>? configure = null)
        => builder.AddAmazonSqsTransport(TransportNames.AmazonSqs, configure);

    /// <summary>Add Amazon SQS transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonSqsTransport(this EventBusBuilder builder, string name, Action<AmazonSqsTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<AmazonSqsConfigureOptions>();
        return builder.AddTransport<AmazonSqsTransport, AmazonSqsTransportOptions>(name, configure);
    }
}
