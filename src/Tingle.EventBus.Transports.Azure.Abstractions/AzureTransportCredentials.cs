using Azure.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Abstractions for authenticating Azure-based transports without a connection strings.
    /// </summary>
    public abstract class AzureTransportCredentials
    {
        /// <summary>
        /// Credential used to authenticate requests.
        /// Depending on the transport resources, the relevant permissions must be available in the provided credential.
        /// For example to access Azure Blob Storage, you need <c>Blob Storage Container Contributor permission</c>.
        /// For more details on assigning tokens,
        /// see the <see href="https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/identity/Azure.Identity/README.md">official Azure SDK identity docs.</see>
        /// </summary>
        public TokenCredential? TokenCredential { get; set; }
    }
}
