using Azure.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Credentials for Azure Service Bus transport backed by a <see cref="TokenCredential"/>.
    /// </summary>
    public class AzureServiceBusTransportCredentials : AzureTransportCredentials
    {
        /// <summary>
        /// The fully qualified Service Bus namespace to connect to.
        /// This is likely to be similar to {yournamespace}.servicebus.windows.net.
        /// </summary>
        public string? FullyQualifiedNamespace { get; set; }
    }
}
