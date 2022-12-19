using Azure.Identity;
using ConfigSample;

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
                           ((AzureServiceBusTransportCredentials)options.Credentials).TokenCredential = credential;
                       });
                   });
               })
               .Build();

await host.RunAsync();
