using Azure.Identity;
using ConfigSample;
using Tingle.EventBus.Transports.Azure.ServiceBus;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.AddConsumer<VehicleTelemetryEventsConsumer>();
                       builder.AddConsumer<VisualsUploadedConsumer>();

                       // Add transports
                       builder.AddAzureServiceBusTransport();
                       builder.AddInMemoryTransport("in-memory-images");
                       builder.AddInMemoryTransport("in-memory-videos");

                       // Transport specific configuration
                       var credential = new DefaultAzureCredential();
                       builder.Services.PostConfigure<AzureServiceBusTransportOptions>(
                           name: AzureServiceBusDefaults.Name,
                           configureOptions: o => ((AzureServiceBusTransportCredentials)o.Credentials).TokenCredential = credential);
                   });

                   services.AddHostedService<VisualsProducerService>();
               })
               .Build();

await host.RunAsync();
