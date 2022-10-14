using AnyOfTypes;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Abstraction for options of Azure-based transports
/// </summary>
public abstract class AzureTransportOptions<TCredential> : EventBusTransportOptions where TCredential : AzureTransportCredentials
{
    /// <summary>
    /// Authentication credentials.
    /// This can either be a connection string or <typeparamref name="TCredential"/>.
    /// </summary>
    public AnyOf<TCredential, string> Credentials { get; set; }
}
