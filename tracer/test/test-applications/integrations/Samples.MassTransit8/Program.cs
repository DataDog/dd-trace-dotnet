using MassTransit;
using Samples.MassTransit8;
using Samples.MassTransit8.Consumers;

// Transport selection via environment variable: "rabbitmq" (default) or "amazonsqs"
var transport = Environment.GetEnvironmentVariable("MASSTRANSIT_TRANSPORT")?.ToLowerInvariant() ?? "rabbitmq";

Console.WriteLine($"MassTransit 8 Sample - Using transport: {transport}");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GettingStartedConsumer>();

    switch (transport)
    {
        case "amazonsqs":
            ConfigureAmazonSqs(x);
            break;
        case "rabbitmq":
        default:
            ConfigureRabbitMq(x);
            break;
    }
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();

void ConfigureRabbitMq(IBusRegistrationConfigurator x)
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
}

void ConfigureAmazonSqs(IBusRegistrationConfigurator x)
{
    x.UsingAmazonSqs((context, cfg) =>
    {
        // Use LocalStack for local testing (default endpoint)
        var localStackEndpoint = Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ?? "http://localhost:4566";
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

        cfg.Host(region, h =>
        {
            // Configure SQS client for LocalStack
            h.Config(new Amazon.SQS.AmazonSQSConfig
            {
                ServiceURL = localStackEndpoint
            });

            // Configure SNS client for LocalStack (MassTransit uses SNS for pub/sub topics)
            h.Config(new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = localStackEndpoint
            });

            // LocalStack doesn't require real credentials
            h.AccessKey("test");
            h.SecretKey("test");
        });

        cfg.ConfigureEndpoints(context);
    });
}
