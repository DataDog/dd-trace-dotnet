using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Consumers;

public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    private readonly ILogger<SubmitOrderConsumer> _logger;
    private readonly MessageCompletionTracker _tracker;

    public SubmitOrderConsumer(ILogger<SubmitOrderConsumer> logger, MessageCompletionTracker tracker)
    {
        _logger = logger;
        _tracker = tracker;
    }

    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        try
        {
            _logger.LogInformation("Received SubmitOrder: OrderId={OrderId}, Customer={Customer}, Amount={Amount}",
                context.Message.OrderId,
                context.Message.CustomerName,
                context.Message.TotalAmount);

            // Simulate order validation
            await Task.Delay(100);

            // Publish event that order was submitted
            await context.Publish(new OrderSubmitted(
                context.Message.OrderId,
                context.Message.CustomerName,
                context.Message.TotalAmount,
                DateTime.UtcNow));

            _logger.LogInformation("Order {OrderId} submitted successfully", context.Message.OrderId);
        }
        finally
        {
            _tracker.MessageCompleted(nameof(SubmitOrder));
        }
    }
}
