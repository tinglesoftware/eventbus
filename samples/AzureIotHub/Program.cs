using AzureIotHub;
using Tingle.EventBus.Configuration;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.Configure(o => o.ConfigureEvent<MyIotHubEvent>(reg => reg.ConfigureAsIotHubEvent(configuration["IotHubEventHubName"])));

                       builder.AddConsumer<AzureIotEventsConsumer>();

                       // Transport specific configuration
                       builder.AddAzureEventHubsTransport(options =>
                       {
                           options.Credentials = configuration.GetConnectionString("EventHub");
                           options.BlobStorageCredentials = configuration.GetConnectionString("AzureStorage");
                       });
                   });
               })
               .Build();

await host.RunAsync();
