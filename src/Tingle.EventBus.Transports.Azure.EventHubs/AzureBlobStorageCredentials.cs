﻿using Azure.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Credentials for Azure Blob Storage backed by a <see cref="TokenCredential"/>.
/// </summary>
public class AzureBlobStorageCredentials : AzureTransportCredentials
{
    /// <summary>
    /// A <see cref="Uri"/> referencing the blob service.
    /// This is likely to be similar to "https://{account_name}.blob.core.windows.net".
    /// </summary>
    public Uri? ServiceUrl { get; set; }
}
