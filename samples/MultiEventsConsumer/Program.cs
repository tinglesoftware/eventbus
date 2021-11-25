var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddDistributedMemoryCache();

                   services.AddEventBus(builder =>
                   {
                       builder.UseDefaultXmlSerializer();

                       // Transport agnostic configuration
                       builder.Configure(o =>
                       {
                           o.Naming.Scope = "dev"; // queues will be prefixed by 'dev'
                           o.Naming.UseFullTypeNames = false;
                       });
                       builder.AddConsumer<MultiEventsConsumer.MultiEventsConsumer>();

                       // Transport specific configuration
                       builder.AddInMemoryTransport();
                   });

                   services.AddHostedService<MultiEventsConsumer.DummyProducerService>();
               })
               .Build();

await host.RunAsync();
