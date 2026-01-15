using MassTransit;
using Samples.MassTransit7;
using Samples.MassTransit7.Consumers;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<GettingStartedConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddHostedService<Worker>();
    });

var host = builder.Build();
await host.RunAsync();
