using CustomEventSerializer;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddEventBus(builder =>
                   {
                       // Transport agnostic configuration
                       builder.Configure(o =>
                       {
                           o.Naming.Scope = "dev"; // queues will be prefixed by 'dev'
                           o.Naming.UseFullTypeNames = false;
                       });
                       builder.AddConsumer<AzureDevOpsEventsConsumer>();

                       // Transport specific configuration
                       builder.AddAzureQueueStorageTransport("UseDevelopmentStorage=true;");
                   });
               })
               .Build();

await host.RunAsync();
