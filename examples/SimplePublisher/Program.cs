using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SimplePublisher
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

                        // Transport specific configuration
                        builder.AddAzureQueueStorageTransport("UseDevelopmentStorage=true;");
                    });

                    services.AddHostedService<PublisherService>();
                });
    }
}
