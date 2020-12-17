using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SimpleConsumer
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
                        builder.Configure(options => options.Scope = "dev");
                        builder.Configure(o => o.UseFullTypeNames = false);
                        builder.Subscribe<SampleEventConsumer>();

                        // Transport specific configuration
                        builder.AddAzureQueueStorage(options => options.ConnectionString = "UseDevelopmentStorage=true;");
                    });
                });
    }
}
