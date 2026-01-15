using MassTransit;
using Samples.MassTransit7.Contracts;

namespace Samples.MassTransit7;

public class Worker : BackgroundService
{
    readonly IBusControl _busControl;
    readonly ILogger<Worker> _logger;

    public Worker(IBusControl busControl, ILogger<Worker> logger)
    {
        _busControl = busControl;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Filter injection now happens at configuration time in Program.cs
        // See: ConfigurationTimeInjector.InjectDuringConfiguration()

        _logger.LogInformation("Starting the bus...");
        await _busControl.StartAsync(stoppingToken);

        _logger.LogInformation("Waiting for bus to be ready...");
        await Task.Delay(2000, stoppingToken);
        _logger.LogInformation("Starting to publish messages...");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Publishing message...");
                await _busControl.Publish(new GettingStartedMessage { Value = $"The time is {DateTimeOffset.Now}" }, stoppingToken);

                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            await _busControl.StopAsync(stoppingToken);
        }
    }
}
