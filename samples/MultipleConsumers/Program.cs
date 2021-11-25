using MultipleConsumers;
using Tingle.EventBus.Configuration;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddEventBus(builder =>
                   {
                       builder.UseDefaultNewtonsoftJsonSerializer();

                       // Transport agnostic configuration
                       builder.Configure(o =>
                       {
                           o.Naming.Scope = "dev"; // queues will be prefixed by 'dev'
                           o.Naming.UseFullTypeNames = false;
                       });
                       builder.AddConsumer<FirstEventConsumer>();
                       builder.AddConsumer<SecondEventConsumer>();

                       // Transport specific configuration
                       builder.AddInMemoryTransport(o =>
                       {
                           // default to Broadcast kind so that we get pub-sub behaviour
                           o.DefaultEntityKind = EntityKind.Broadcast;
                       });
                   });

                   services.AddHostedService<PublisherService>();
               })
               .Build();

await host.RunAsync();
