﻿using SimpleConsumer;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddSingleton<EventCounter>();
                   services.AddEventBus(builder =>
                   {
                       // Transport agnostic configuration
                       builder.Configure(o =>
                       {
                           o.Naming.Scope = "dev"; // queues will be prefixed by 'dev'
                           o.Naming.UseFullTypeNames = false;
                       });
                       builder.AddConsumer<SampleEventConsumer>();

                       // Transport specific configuration
                       builder.AddAzureQueueStorageTransport(o => o.Credentials = "UseDevelopmentStorage=true;");
                   });
               })
               .Build();

await host.RunAsync();
