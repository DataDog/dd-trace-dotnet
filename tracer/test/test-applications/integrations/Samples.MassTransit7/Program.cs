using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Contracts;
using Samples.MassTransit7.Consumers;
using Samples.MassTransit7.Sagas;

Console.WriteLine("MassTransit 7 Sample - Testing all transports sequentially");

// Run each transport one after another
await RunWithTransport("inmemory", ConfigureInMemory);
await RunWithTransport("rabbitmq", ConfigureRabbitMq);
await RunWithTransport("amazonsqs", ConfigureAmazonSqs);

// Run saga test with in-memory transport
await RunSagaTest();

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
        await Task.Delay(500);

        Console.WriteLine($"[{transportName}] Publishing message...");
        await busControl.Publish(new GettingStartedMessage { Value = $"Hello from {transportName} at {DateTimeOffset.Now}" });

        // Wait for the message to be consumed
        Console.WriteLine($"[{transportName}] Waiting for message to be consumed...");
        await Task.Delay(1000);

        Console.WriteLine($"[{transportName}] Test completed successfully!");
    }
    finally
    {
        Console.WriteLine($"[{transportName}] Stopping the bus...");
        await busControl.StopAsync();

        // Give time for cleanup before next transport
        await Task.Delay(500);
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

async Task RunSagaTest()
{
    Console.WriteLine("\n========== Testing SAGA STATE MACHINE ==========");

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddMassTransit(x =>
    {
        // Register the saga state machine with in-memory repository
        x.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .InMemoryRepository();

        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var busControl = serviceProvider.GetRequiredService<IBusControl>();

    try
    {
        Console.WriteLine("[saga] Starting the bus...");
        await busControl.StartAsync();

        // Give the bus time to fully initialize
        await Task.Delay(500);

        // Create an order ID for the saga
        var orderId = Guid.NewGuid();
        Console.WriteLine($"[saga] Testing order saga with OrderId: {orderId}");

        // Step 1: Submit the order (Initial -> Submitted)
        Console.WriteLine("[saga] Publishing OrderSubmitted event...");
        await busControl.Publish(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerName = "Test Customer",
            Amount = 99.99m
        });
        await Task.Delay(500);

        // Step 2: Accept the order (Submitted -> Accepted)
        Console.WriteLine("[saga] Publishing OrderAccepted event...");
        await busControl.Publish(new OrderAccepted { OrderId = orderId });
        await Task.Delay(500);

        // Step 3: Complete the order (Accepted -> Completed)
        Console.WriteLine("[saga] Publishing OrderCompleted event...");
        await busControl.Publish(new OrderCompleted { OrderId = orderId });
        await Task.Delay(500);

        Console.WriteLine("[saga] Saga test completed successfully!");
    }
    finally
    {
        Console.WriteLine("[saga] Stopping the bus...");
        await busControl.StopAsync();
        await Task.Delay(500);
    }
}
