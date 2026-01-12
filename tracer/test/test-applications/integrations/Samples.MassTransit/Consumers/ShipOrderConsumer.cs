using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit.Messages;

namespace Samples.MassTransit.Consumers;

public class ShipOrderConsumer : IConsumer<ShipOrder>
{
    private readonly ILogger<ShipOrderConsumer> _logger;

    public ShipOrderConsumer(ILogger<ShipOrderConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShipOrder> context)
    {
        _logger.LogInformation("Shipping order {OrderId} to {Address}",
            context.Message.OrderId,
            context.Message.ShippingAddress);

        // Simulate shipping process
        await Task.Delay(150);

        // Generate deterministic tracking number from order ID
        var trackingNumber = $"TRK{context.Message.OrderId.ToString("N")[..6].ToUpper()}";

        await context.Publish(new OrderShipped(
            context.Message.OrderId,
            trackingNumber,
            DateTime.UtcNow));

        _logger.LogInformation("Order {OrderId} shipped with tracking {TrackingNumber}",
            context.Message.OrderId,
            trackingNumber);
    }
}
