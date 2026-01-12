using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Samples.MassTransit.Consumers;
using Samples.MassTransit.Messages;
using Samples.MassTransit.Sagas;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║      MassTransit Comprehensive Feature Demonstration        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure OpenTelemetry - traces will be picked up by Datadog's tracer
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("Samples.MassTransit", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource("MassTransit")); // MassTransit's built-in ActivitySource

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    // Add all consumers
    x.AddConsumer<SubmitOrderConsumer>(cfg =>
    {
        // Configure retry policy for this consumer
        cfg.UseMessageRetry(r => r.Intervals(100, 200, 500));
    });
    x.AddConsumer<ProcessPaymentConsumer>();
    x.AddConsumer<ShipOrderConsumer>();
    x.AddConsumer<InventoryConsumer>();

    // Add request client for Request/Response pattern
    x.AddRequestClient<CheckInventory>();

    // Add saga state machine with in-memory repository
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .InMemoryRepository();

    // Configure the bus
    x.UsingInMemory((context, cfg) =>
    {
        // Configure message retry
        cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000));

        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

// Start the bus
await host.StartAsync();

var busControl = host.Services.GetRequiredService<IBusControl>();
var requestClient = host.Services.GetRequiredService<IRequestClient<CheckInventory>>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 1: Request/Response Pattern");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoRequestResponse(requestClient, logger);
    await Task.Delay(1000);

    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 2: Saga State Machine - Single Successful Order");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoSagaStateMachine(busControl, logger);
    await Task.Delay(2000);

    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 3: Multiple Orders in Parallel");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoParallelProcessing(busControl, logger);
    await Task.Delay(2000);

    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 4: Payment Failures & Error Handling");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoErrorHandling(busControl, logger);
    await Task.Delay(2000);

    Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║            All demonstrations completed!                     ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during demonstration");
}
finally
{
    await host.StopAsync();
}

return;

// ============================================================================
// Demo Functions
// ============================================================================

static async Task DemoRequestResponse(IRequestClient<CheckInventory> client, ILogger logger)
{
    logger.LogInformation("Making Request/Response call for inventory check...");

    try
    {
        var response = await client.GetResponse<InventoryResult>(
            new CheckInventory("Laptop", 5),
            timeout: RequestTimeout.After(s: 5));

        logger.LogInformation(
            "Inventory Response: Available={Available}, Quantity={Quantity}",
            response.Message.Available,
            response.Message.AvailableQuantity);
    }
    catch (RequestTimeoutException)
    {
        logger.LogError("Request timed out");
    }
}

static async Task DemoSagaStateMachine(IBusControl bus, ILogger logger)
{
    logger.LogInformation("Submitting a single order to demonstrate saga workflow...");
    logger.LogInformation("Saga will coordinate: Order → Payment → Shipping → Completion");

    var orderId = Guid.NewGuid();
    await bus.Publish(new OrderSubmitted(
        orderId,
        "Alice Johnson",
        99.99m,
        DateTime.UtcNow));

    logger.LogInformation("Order submitted - watch the saga orchestrate the workflow");
}

static async Task DemoParallelProcessing(IBusControl bus, ILogger logger)
{
    logger.LogInformation("Processing multiple orders in parallel...");

    var orders = new[]
    {
        (Customer: "Bob Wilson", Amount: 75.50m),
        (Customer: "Carol Martinez", Amount: 125.00m),
        (Customer: "David Lee", Amount: 89.99m)
    };
    
    var tasks = new List<Task>();

    foreach (var order in orders)
    {
        var orderId = Guid.NewGuid();
        var task = bus.Publish(new OrderSubmitted(
            orderId,
            order.Customer,
            order.Amount,
            DateTime.UtcNow));
        tasks.Add(task);
    }

    await Task.WhenAll(tasks);
    logger.LogInformation("All {Count} orders submitted in parallel", orders.Length);
}

static async Task DemoErrorHandling(IBusControl bus, ILogger logger)
{
    logger.LogInformation("Demonstrating error handling with payment failures...");

    // Submit multiple orders - amounts >= $200 will fail payment (deterministic)
    var amounts = new[] { 100m, 150m, 200m, 250m, 175m };

    for (int i = 0; i < amounts.Length; i++)
    {
        var orderId = Guid.NewGuid();
        await bus.Publish(new OrderSubmitted(
            orderId,
            $"Customer {i + 1}",
            amounts[i],
            DateTime.UtcNow));
    }

    logger.LogInformation("Submitted 5 orders - 2 will fail payment (>=$200) and trigger saga cancellations");
}
