using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Consumers;

public class ProcessPaymentConsumer : IConsumer<ProcessPayment>
{
    private readonly ILogger<ProcessPaymentConsumer> _logger;
    private readonly MessageCompletionTracker _tracker;

    public ProcessPaymentConsumer(ILogger<ProcessPaymentConsumer> logger, MessageCompletionTracker tracker)
    {
        _logger = logger;
        _tracker = tracker;
    }

    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        try
        {
            _logger.LogInformation("Processing payment for Order {OrderId}, Amount={Amount}",
                context.Message.OrderId,
                context.Message.Amount);

            // Simulate payment processing
            await Task.Delay(200);

            // Deterministic behavior: payments under $200 succeed, others fail
            var success = context.Message.Amount < 200m;

            if (success)
            {
                // Generate deterministic transaction ID from order ID
                var transactionId = $"TXN{context.Message.OrderId.ToString("N")[..9].ToUpper()}";
                await context.Publish(new PaymentProcessed(
                    context.Message.OrderId,
                    true,
                    transactionId));

                _logger.LogInformation("Payment processed successfully for Order {OrderId}, TransactionId={TransactionId}",
                    context.Message.OrderId,
                    transactionId);
            }
            else
            {
                await context.Publish(new PaymentFailed(
                    context.Message.OrderId,
                    "Insufficient funds"));

                _logger.LogWarning("Payment failed for Order {OrderId}", context.Message.OrderId);
            }
        }
        finally
        {
            _tracker.MessageCompleted(nameof(ProcessPayment));
        }
    }
}
