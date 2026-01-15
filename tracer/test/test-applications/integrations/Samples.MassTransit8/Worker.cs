using MassTransit;
using Samples.MassTransit8.Contracts;

namespace Samples.MassTransit8;

public class Worker : BackgroundService
{
    readonly IBus _bus;
    readonly ILogger<Worker> _logger;

    public Worker(IBus bus, ILogger<Worker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Waiting for bus to be ready...");
        await Task.Delay(2000, stoppingToken);
        _logger.LogInformation("Starting to publish messages...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Publishing message...");
            await _bus.Publish(new GettingStartedMessage { Value = $"The time is {DateTimeOffset.Now}" }, stoppingToken);

            await Task.Delay(1000, stoppingToken);
        }
    }
}
