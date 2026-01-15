using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit7.Messages;

namespace Samples.MassTransit7.Consumers;

public class ProcessPaymentConsumer : IConsumer<ProcessPayment>
{
    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(ILogger<ProcessPaymentConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        _logger.LogInformation("Processing payment for Order {OrderId}, Amount={Amount}",
            context.Message.OrderId, context.Message.Amount);

        await Task.Delay(200);

        var transactionId = $"TXN{context.Message.OrderId.ToString("N")[..9].ToUpper()}";
        await context.Publish(new PaymentProcessed(context.Message.OrderId, true, transactionId));

        _logger.LogInformation("Payment processed for Order {OrderId}", context.Message.OrderId);
    }
}
