using MassTransit;
using Samples.MassTransit7;
using Samples.MassTransit7.Consumers;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<GettingStartedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("localhost", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddHostedService<Worker>();
    });

var host = builder.Build();
await host.RunAsync();
