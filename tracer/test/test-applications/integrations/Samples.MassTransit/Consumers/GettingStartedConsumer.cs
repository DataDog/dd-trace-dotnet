using MassTransit;
using Samples.MassTransit.Contracts;

namespace Samples.MassTransit.Consumers;

public class GettingStartedConsumer : IConsumer<GettingStartedMessage>
{
    readonly ILogger<GettingStartedConsumer> _logger;

    public GettingStartedConsumer(ILogger<GettingStartedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<GettingStartedMessage> context)
    {
        Console.WriteLine($"CONSUMER: {context.Message.Value}");
        _logger.LogInformation("Received Text: {Text}", context.Message.Value);
        return Task.CompletedTask;
    }
}
