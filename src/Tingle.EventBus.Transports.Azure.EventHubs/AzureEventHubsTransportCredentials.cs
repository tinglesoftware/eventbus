using Azure.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Credentials for Azure EventHubs transport backed by a <see cref="TokenCredential"/>.
/// </summary>
public class AzureEventHubsTransportCredentials : AzureTransportCredentials
{
    /// <summary>
    /// The fully qualified Event Hubs namespace to connect to.
    /// This is likely to be similar to {your_namespace}.servicebus.windows.net.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }
}
