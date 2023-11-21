using CustomEventConfigurator;
using Tingle.EventBus.Configuration;

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
                       builder.AddConsumer<SampleConsumer1>();
                       builder.AddConsumer<SampleConsumer2>();

                       // Setup extra configurators
                       // You can have as many as you like, these are called after the default one.
                       builder.Services.AddSingleton<IEventBusConfigurator, MyConfigurator>();

                       // Transport specific configuration
                       builder.AddAzureQueueStorageTransport(o => o.Credentials = "UseDevelopmentStorage=true;");
                   });
               })
               .Build();

await host.RunAsync();
