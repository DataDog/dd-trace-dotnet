using MassTransit;
using Microsoft.Extensions.Logging;
using Samples.MassTransit.Contracts;

namespace Samples.MassTransit.Consumers;

public class GettingStartedWithInMemoryConsumer : IConsumer<GettingStartedWithInMemory>
{
    readonly ILogger<GettingStartedWithInMemoryConsumer> _logger;

    public GettingStartedWithInMemoryConsumer(ILogger<GettingStartedWithInMemoryConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<GettingStartedWithInMemory> context)
    {
        Console.WriteLine($"CONSUMER: {context.Message.Value}");
        _logger.LogInformation("Received Text: {Text}", context.Message.Value);
        TestSignal.Set(context.Message.Value);
        return Task.CompletedTask;
    }
}

public class GettingStartedWithRabbitMqConsumer : IConsumer<GettingStartedWithRabbitMq>
{
    readonly ILogger<GettingStartedWithRabbitMqConsumer> _logger;

    public GettingStartedWithRabbitMqConsumer(ILogger<GettingStartedWithRabbitMqConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<GettingStartedWithRabbitMq> context)
    {
        Console.WriteLine($"CONSUMER: {context.Message.Value}");
        _logger.LogInformation("Received Text: {Text}", context.Message.Value);
        TestSignal.Set(context.Message.Value);
        return Task.CompletedTask;
    }
}

public class GettingStartedWithSqsConsumer : IConsumer<GettingStartedWithSqs>
{
    readonly ILogger<GettingStartedWithSqsConsumer> _logger;

    public GettingStartedWithSqsConsumer(ILogger<GettingStartedWithSqsConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<GettingStartedWithSqs> context)
    {
        Console.WriteLine($"CONSUMER: {context.Message.Value}");
        _logger.LogInformation("Received Text: {Text}", context.Message.Value);
        TestSignal.Set(context.Message.Value);
        return Task.CompletedTask;
    }
}
