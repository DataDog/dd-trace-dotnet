using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Consumers;

public class ShipOrderConsumer : IConsumer<ShipOrder>
{
    private readonly ILogger<ShipOrderConsumer> _logger;
    private readonly MessageCompletionTracker _tracker;

    public ShipOrderConsumer(ILogger<ShipOrderConsumer> logger, MessageCompletionTracker tracker)
    {
        _logger = logger;
        _tracker = tracker;
    }

    public async Task Consume(ConsumeContext<ShipOrder> context)
    {
        try
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
        finally
        {
            _tracker.MessageCompleted(nameof(ShipOrder));
        }
    }
}
