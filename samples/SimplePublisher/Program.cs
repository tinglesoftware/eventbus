using SimplePublisher;

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

                       // Transport specific configuration
                       builder.AddAzureQueueStorageTransport(o => o.Credentials = "UseDevelopmentStorage=true;");
                   });

                   services.AddHostedService<PublisherService>();
               })
               .Build();

await host.RunAsync();
