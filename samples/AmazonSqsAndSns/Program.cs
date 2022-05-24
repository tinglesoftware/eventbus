using AmazonSqsAndSns;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddEventBus(builder =>
        {
            builder.AddConsumer<DoorOpenedConsumer>();

            // Transport specific configuration
            builder.AddAmazonSqsTransport(options =>
            {
                options.RegionName = "eu-west-1";
                options.AccessKey = "my-access-key";
                options.SecretKey = "my-secret-key";
            });
        });

        services.AddHostedService<PublisherService>();
    })
    .Build();

await host.RunAsync();
