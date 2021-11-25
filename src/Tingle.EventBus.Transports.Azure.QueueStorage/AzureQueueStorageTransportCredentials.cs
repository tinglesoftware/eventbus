using Azure.Core;
using System;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Credentials for Azure Queue Storage transport backed by a <see cref="TokenCredential"/>.
/// </summary>
public class AzureQueueStorageTransportCredentials : AzureTransportCredentials
{
    /// <summary>
    /// A <see cref="Uri"/> referencing the queue service.
    /// This is likely to be similar to "https://{account_name}.queue.core.windows.net".
    /// </summary>
    public Uri? ServiceUrl { get; set; }
}
