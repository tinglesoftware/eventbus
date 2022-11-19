using MultipleDifferentTransports;
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

                       // Transport specific configuration
                       builder.AddAzureEventHubsTransport(options =>
                       {
                           options.Credentials = configuration.GetConnectionString("EventHub")!;
                           options.BlobStorageCredentials = configuration.GetConnectionString("AzureStorage")!;
                       });

                       // Transport specific configuration
                       builder.AddAzureServiceBusTransport(options =>
                       {
                           options.Credentials = configuration.GetConnectionString("ServiceBus")!;
                           options.DefaultEntityKind = EntityKind.Queue; // required if using the basic SKU (does not support topics)
                       });
                   });
               })
               .Build();

await host.RunAsync();
