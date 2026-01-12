using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7;
using Samples.MassTransit7.Consumers;
using Samples.MassTransit7.Messages;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   MassTransit 7 Comprehensive Feature Demonstration         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

// Register message completion tracker as singleton
var tracker = new MessageCompletionTracker();
builder.Services.AddSingleton(tracker);

// Configure logging - enable Debug for MassTransit to see what's happening
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("MassTransit", LogLevel.Debug);
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

// Get services
var bus = host.Services.GetRequiredService<IBus>();
var busControl = host.Services.GetRequiredService<IBusControl>();
var requestClient = host.Services.GetRequiredService<IRequestClient<CheckInventory>>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Explicitly start the bus control (this might be needed for MassTransit 7)
Console.WriteLine("[DEBUG] Starting bus control...");
await busControl.StartAsync();
Console.WriteLine("[DEBUG] Bus control started");

// DEBUG: Print actual bus type
Console.WriteLine($"[DEBUG] Bus concrete type: {bus.GetType().FullName}");
Console.WriteLine($"[DEBUG] Bus assembly: {bus.GetType().Assembly.GetName().Name} v{bus.GetType().Assembly.GetName().Version}");

var publishMethods = bus.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
    .Where(m => m.Name == "Publish" || m.Name.Contains("Publish"))
    .Where(m => !m.IsGenericMethod)  // Only non-generic methods
    .Take(10);
Console.WriteLine($"[DEBUG] Found {publishMethods.Count()} non-generic Publish methods:");
foreach (var method in publishMethods)
{
    Console.WriteLine($"[DEBUG] Method: {method.DeclaringType?.FullName}.{method.Name}");
    var parameters = method.GetParameters();
    Console.WriteLine($"[DEBUG]   Params ({parameters.Length}): {string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
    Console.WriteLine($"[DEBUG]   Return: {method.ReturnType.Name}");
}

// Check interface map
var iface = bus.GetType().GetInterface("MassTransit.IPublishEndpoint");
if (iface != null)
{
    var interfaceMap = bus.GetType().GetInterfaceMap(iface);
    Console.WriteLine($"[DEBUG] IPublishEndpoint interface map:");
    for (int i = 0; i < Math.Min(3, interfaceMap.InterfaceMethods.Length); i++)
    {
        Console.WriteLine($"[DEBUG]   Interface: {interfaceMap.InterfaceMethods[i]}");
        Console.WriteLine($"[DEBUG]   Target:    {interfaceMap.TargetMethods[i].DeclaringType?.FullName}.{interfaceMap.TargetMethods[i].Name}");
    }
}
Console.WriteLine();

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
    await DemoPublishSubscribe(bus, logger, tracker);
    Console.WriteLine("[DEBUG] Waiting for all consumers to complete...");
    await tracker.WaitForAll(TimeSpan.FromSeconds(15));
    Console.WriteLine("[DEBUG] All consumers completed");
    // Small delay to ensure all RECEIVE logs are flushed
    await Task.Delay(100);

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

static async Task DemoPublishSubscribe(IBus bus, ILogger logger, MessageCompletionTracker tracker)
{
    logger.LogInformation("Publishing order commands to demonstrate pub/sub workflow...");

    // Expect 3 messages to be consumed: SubmitOrder, ProcessPayment, ShipOrder
    tracker.Reset();
    tracker.ExpectMessages(nameof(SubmitOrder), 1);
    tracker.ExpectMessages(nameof(ProcessPayment), 1);
    tracker.ExpectMessages(nameof(ShipOrder), 1);

    var orderId = Guid.NewGuid();

    // Publish all commands
    Console.WriteLine($"[TEST] Calling generic Publish<SubmitOrder>");
    await bus.Publish(new SubmitOrder(orderId, "Alice Johnson", 99.99m, new List<OrderItem>
    {
        new OrderItem("Laptop", 1, 99.99m)
    }));

    Console.WriteLine($"[TEST] Calling generic Publish<ProcessPayment>");
    await bus.Publish(new ProcessPayment(orderId, 99.99m));

    Console.WriteLine($"[TEST] Calling generic Publish<ShipOrder>");
    await bus.Publish(new ShipOrder(orderId, "123 Main St"));

    logger.LogInformation("Order commands submitted - waiting for consumers to process them");
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
