using Azure.Identity;
using AzureManagedIdentity;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports.Azure.EventHubs;
using Tingle.EventBus.Transports.Azure.ServiceBus;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.Configure(o =>
                       {
                           o.DefaultTransportName = AzureServiceBusDefaults.Name;

                           o.ConfigureEvent<VehicleTelemetryEvent>(reg =>
                           {
                               reg.ConfigureAsIotHubEvent(configuration["IotHubEventHubName"]!)
                                  .UseIotHubEventSerializer();

                               reg.TransportName = AzureEventHubsDefaults.Name; // you can also use EventTransportNameAttribute on the event type declaration
                           });
                       });

                       builder.AddConsumer<VehicleTelemetryEventsConsumer>();

                       var credential = new DefaultAzureCredential();

                       // Transport specific configuration
                       builder.AddAzureEventHubsTransport(options =>
                       {
                           options.Credentials = new AzureEventHubsTransportCredentials
                           {
                               TokenCredential = credential,
                               FullyQualifiedNamespace = "{your_namespace}.servicebus.windows.net"
                           };
                           options.BlobStorageCredentials = new AzureBlobStorageCredentials
                           {
                               TokenCredential = credential,
                               BlobServiceUrl = new Uri("https://{account_name}.blob.core.windows.net"),
                           };
                       });

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
