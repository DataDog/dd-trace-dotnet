using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Consumers;

// Request/Response consumer
public class InventoryConsumer : IConsumer<CheckInventory>
{
    private readonly ILogger<InventoryConsumer> _logger;

    public InventoryConsumer(ILogger<InventoryConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckInventory> context)
    {
        _logger.LogInformation("Checking inventory for {ProductName}, Quantity={Quantity}",
            context.Message.ProductName,
            context.Message.Quantity);

        // Simulate inventory check
        await Task.Delay(50);

        // Deterministic behavior: all items are available with extra stock
        var available = true;
        var availableQuantity = context.Message.Quantity + 10;

        // Respond to the request
        await context.RespondAsync(new InventoryResult(available, availableQuantity));

        _logger.LogInformation("Inventory check for {ProductName}: Available={Available}, Quantity={Quantity}",
            context.Message.ProductName,
            available,
            availableQuantity);
    }
}
