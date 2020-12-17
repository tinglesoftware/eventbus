namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Queue Storage based event bus.
    /// </summary>
    public class AzureQueueStorageOptions
    {
        /// <summary>
        /// The connection string to Azure Queue Storage.
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
