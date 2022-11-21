using Azure.Identity;
using AzureManagedIdentity;
using Tingle.EventBus.Configuration;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.AddConsumer<VehicleTelemetryEventsConsumer>();

                       var credential = new DefaultAzureCredential();

                       // Transport specific configuration
                       builder.AddAzureServiceBusTransport(options =>
                       {
                           options.Credentials = new AzureServiceBusTransportCredentials
                           {
                               TokenCredential = credential,
                               FullyQualifiedNamespace = "{your_namespace}.servicebus.windows.net"
                           };
                           options.DefaultEntityKind = EntityKind.Queue; // required if using the basic SKU (does not support topics)
                       });
                   });
               })
               .Build();

await host.RunAsync();
