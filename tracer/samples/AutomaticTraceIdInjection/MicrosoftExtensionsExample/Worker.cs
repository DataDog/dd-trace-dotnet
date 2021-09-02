using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            using (_logger.BeginScope(new Dictionary<string, object>{{"order-number", 1024}}))
            {
                _logger.LogInformation("Message before a trace.");

                using (Tracer.Instance.StartActive("Microsoft.Extensions.Example - Worker.ExecuteAsync()"))
                {
                    _logger.LogInformation("Message during a trace.");

                    using (Tracer.Instance.StartActive("Microsoft.Extensions.Example - Nested span"))
                    {
                        _logger.LogInformation("Message during a child span.");
                    }
                }
                _logger.LogInformation("Message after a trace.");
            }

            await Task.Delay(1_000, stoppingToken);
        }
    }
}
