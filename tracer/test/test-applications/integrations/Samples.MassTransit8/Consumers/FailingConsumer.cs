using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit8.Contracts;

namespace Samples.MassTransit8.Consumers;

public class FailingConsumer : IConsumer<FailingMessage>
{
    readonly ILogger<FailingConsumer> _logger;

    public FailingConsumer(ILogger<FailingConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<FailingMessage> context)
    {
        _logger.LogInformation("FailingConsumer received: {Text} - About to throw exception", context.Message.Value);
        throw new InvalidOperationException($"Intentional failure for testing: {context.Message.Value}");
    }
}
