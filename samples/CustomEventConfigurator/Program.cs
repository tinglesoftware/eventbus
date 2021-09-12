using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tingle.EventBus.Configuration;

namespace CustomEventConfigurator
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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
                        builder.Services.AddSingleton<IEventConfigurator, MyConfigurator>();

                        // Transport specific configuration
                        builder.AddAzureQueueStorageTransport("UseDevelopmentStorage=true;");
                    });
                });
    }
}
