using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CustomEventSerializer
{
    public class Program
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
                        builder.AddConsumer<AzureDevOpsEventsConsumer>();

                        // setup extra serializers
                        builder.Services.AddSingleton<AzureDevOpsEventSerializer>();

                        // Transport specific configuration
                        builder.AddAzureQueueStorageTransport("UseDevelopmentStorage=true;");
                    });
                });
    }
}
