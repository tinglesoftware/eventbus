using AzureIotHub;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.AddConsumer<AzureIotEventsConsumer>();

                       // Transport specific configuration
                       builder.AddAzureEventHubsTransport(options =>
                       {
                           options.Credentials = configuration.GetConnectionString("EventHub");
                           options.BlobStorageCredentials = "UseDevelopmentStorage=true;";
                       });
                   });
               })
               .Build();

await host.RunAsync();
