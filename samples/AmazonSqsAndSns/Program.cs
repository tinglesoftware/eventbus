using AmazonSqsAndSns;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddEventBus(builder =>
        {
            
        });
    })
    .Build();

await host.RunAsync();
