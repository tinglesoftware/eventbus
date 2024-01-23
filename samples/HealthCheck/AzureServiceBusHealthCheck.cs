using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Transports.Azure.ServiceBus;

namespace HealthCheck;

internal class AzureServiceBusHealthCheck(IOptionsMonitor<AzureServiceBusTransportOptions> optionsMonitor) : IHealthCheck
{
    private const string QueueName = "health-check";

    private readonly AzureServiceBusTransportOptions options = optionsMonitor?.Get(AzureServiceBusDefaults.Name) ?? throw new ArgumentNullException(nameof(optionsMonitor));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var cred = (AzureServiceBusTransportCredentials)options.Credentials.CurrentValue;
            var managementClient = new ServiceBusAdministrationClient(
                fullyQualifiedNamespace: cred.FullyQualifiedNamespace,
                credential: cred.TokenCredential);

            _ = await managementClient.GetQueueRuntimePropertiesAsync(QueueName, cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore long running calls
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}

