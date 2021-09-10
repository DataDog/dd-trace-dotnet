using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogsInjection.ILogger
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
#pragma warning disable 618 // ignore obsolete IApplicationLifetime
        private readonly IApplicationLifetime _lifetime;

        public Worker(ILogger<Worker> logger, IApplicationLifetime lifetime, IServiceProvider serviceProvider)
#pragma warning restore 618
        {
            _logger = logger;
            _lifetime = lifetime;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!Startup.AppListening && !stoppingToken.IsCancellationRequested)
            {
                _logger.UninjectedLog("Waiting for app started handling requests");
                await Task.Delay(100, stoppingToken);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.UninjectedLog("Cancellation requested.");
                return;
            }

            using (var scope = Datadog.Trace.Tracer.Instance.StartActive("worker request"))
            {
                try
                {
                    using var serviceScope = _serviceProvider.CreateScope();
                    var httpClientFactory = serviceScope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                    var client = httpClientFactory.CreateClient();

                    _logger.LogInformation("Sending request to self");
                    var response = await client.GetAsync(Startup.ServerAddress, stoppingToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Error sending request, status code did not indicate success");
                        response.EnsureSuccessStatusCode();
                    }

                    _logger.LogInformation("Request sent successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending request");
                }
            }

            _logger.UninjectedLog("Stopping application");

            // trigger app shutdown
            _lifetime.StopApplication();
        }
    }
}
