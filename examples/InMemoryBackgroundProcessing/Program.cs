using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InMemoryBackgroundProcessing
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
                        builder.Subscribe<VideoUploadedConsumer>();

                        // Transport specific configuration
                        builder.AddInMemoryTransport(options=>
                        {
                            // we need to see results fast, for the example. CPU usage does not matter
                            options.EmptyResultsDelay = System.TimeSpan.FromSeconds(5);
                        });
                    });

                    services.AddHostedService<ProducerService>();
                });
    }
}
