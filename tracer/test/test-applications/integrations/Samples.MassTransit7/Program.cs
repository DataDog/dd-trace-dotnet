using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Contracts;
using Samples.MassTransit7.Consumers;

Console.WriteLine("MassTransit 7 Sample - Testing all transports sequentially");

// Run each transport one after another
await RunWithTransport("inmemory", ConfigureInMemory);
await RunWithTransport("rabbitmq", ConfigureRabbitMq);
await RunWithTransport("amazonsqs", ConfigureAmazonSqs);

Console.WriteLine("All transports tested successfully!");

async Task RunWithTransport(string transportName, Action<IBusRegistrationConfigurator> configureTransport)
{
    Console.WriteLine($"\n========== Testing {transportName.ToUpperInvariant()} ==========");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        x.AddConsumer<GettingStartedConsumer>();
        configureTransport(x);
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();

    try
    {
        Console.WriteLine($"[{transportName}] Starting the bus...");
        await busControl.StartAsync();

        // Give the bus time to fully initialize
        await Task.Delay(2000);

        Console.WriteLine($"[{transportName}] Publishing message...");
        await busControl.Publish(new GettingStartedMessage { Value = $"Hello from {transportName} at {DateTimeOffset.Now}" });

        // Wait for the message to be consumed
        Console.WriteLine($"[{transportName}] Waiting for message to be consumed...");
        await Task.Delay(3000);

        Console.WriteLine($"[{transportName}] Test completed successfully!");
    }
    finally
    {
        Console.WriteLine($"[{transportName}] Stopping the bus...");
        await busControl.StopAsync();

        // Give time for cleanup before next transport
        await Task.Delay(1000);
    }
}

void ConfigureInMemory(IBusRegistrationConfigurator x)
{
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
}

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
        // Set LOCALSTACK_ENDPOINT to override (e.g., "http://localhost:4566")
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
