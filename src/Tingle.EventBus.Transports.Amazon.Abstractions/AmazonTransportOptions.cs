using Amazon;
using Amazon.Runtime;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Abstraction for options of AWS-based transports
/// </summary>
public abstract class AmazonTransportOptions : EventBusTransportOptionsBase
{
    /// <summary>
    /// The system name of the region to connect to.
    /// For example <c>eu-west-1</c>.
    /// When not configured, <see cref="Region"/> must be provided.
    /// </summary>
    public string? RegionName { get; set; }

    /// <summary>
    /// The region to connect to.
    /// When not set, <see cref="RegionName"/> is used to set it.
    /// </summary>
    public RegionEndpoint? Region { get; set; }

    /// <summary>
    /// The name of the key granted the requisite access control rights.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// The secret associated with the <see cref="AccessKey"/>.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Credentials for accessing AWS services.
    /// This can be used in place of <see cref="AccessKey"/> and <see cref="SecretKey"/>.
    /// </summary>
    public AWSCredentials? Credentials { get; set; }
}
