using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CustomSerializer
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
                        builder.Configure(options => options.Scope = "dev"); // queues will be prefixed by 'dev'
                        builder.Configure(o => o.UseFullTypeNames = false);
                        builder.Subscribe<AzureDevOpsEventsConsumer>();

                        // setup extra serializers
                        builder.Services.AddSingleton<AzureDevOpsEventSerializer>();

                        // Transport specific configuration
                        builder.AddAzureQueueStorage(options => options.ConnectionString = "UseDevelopmentStorage=true;");
                    });
                });
    }
}
