using Tingle.EventBus.Transports.Amazon.Sqs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Amazon SQS.
/// </summary>
public static class AmazonSqsEventBusBuilderExtensions
{
    /// <summary>Add Amazon SQS transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonSqsTransport(this EventBusBuilder builder, Action<AmazonSqsTransportOptions>? configure = null)
        => builder.AddAmazonSqsTransport(AmazonSqsDefaults.Name, configure);

    /// <summary>Add Amazon SQS transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAmazonSqsTransport(this EventBusBuilder builder, string name, Action<AmazonSqsTransportOptions>? configure = null)
        => builder.AddTransport<AmazonSqsTransport, AmazonSqsTransportOptions, AmazonSqsConfigureOptions>(name, configure);
}
