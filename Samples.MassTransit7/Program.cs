using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Consumers;
using Samples.MassTransit7.Messages;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   MassTransit 7 Comprehensive Feature Demonstration         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure MassTransit 7
builder.Services.AddMassTransit(x =>
{
    // Add all consumers
    x.AddConsumer<SubmitOrderConsumer>();
    x.AddConsumer<ProcessPaymentConsumer>();
    x.AddConsumer<ShipOrderConsumer>();
    x.AddConsumer<InventoryConsumer>();

    // Configure the bus - MassTransit 7 style
    x.UsingInMemory((context, cfg) =>
    {
        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });

    // Add request client for Request/Response pattern
    x.AddRequestClient<CheckInventory>();
});

var host = builder.Build();

// Start the host
await host.StartAsync();

var bus = host.Services.GetRequiredService<IBus>();
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
    Console.WriteLine("Feature 2: Publish/Subscribe - Order Workflow");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoPublishSubscribe(bus, logger);
    await Task.Delay(2000);

    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 3: Multiple Orders in Parallel");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoParallelProcessing(bus, logger);
    await Task.Delay(2000);

    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
    Console.WriteLine("Feature 4: Payment Failures & Error Handling");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    await DemoErrorHandling(bus, logger);
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

static async Task DemoPublishSubscribe(IBus bus, ILogger logger)
{
    logger.LogInformation("Publishing order events to demonstrate pub/sub workflow...");

    var orderId = Guid.NewGuid();
    
    // Publish order submitted event
    await bus.Publish(new OrderSubmitted(orderId, "Alice Johnson", 99.99m, DateTime.UtcNow));
    logger.LogInformation("Order submitted - consumers will process payment and shipping");
}

static async Task DemoParallelProcessing(IBus bus, ILogger logger)
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

static async Task DemoErrorHandling(IBus bus, ILogger logger)
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

    logger.LogInformation("Submitted 5 orders - 2 will fail payment (>=$200)");
}
