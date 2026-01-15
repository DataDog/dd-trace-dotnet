using MassTransit;
using Samples.MassTransit7;
using Samples.MassTransit7.Consumers;
using Samples.MassTransit7.Instrumentation;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<GettingStartedConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                // Reflection-based injection during configuration callback
                // This demonstrates how automatic instrumentation could hook in
                ConfigurationTimeInjector.InjectDuringConfiguration(cfg);

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddHostedService<Worker>();
    });

var host = builder.Build();
await host.RunAsync();
