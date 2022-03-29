using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCoreSmokeTest
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
#pragma warning disable 618 // ignore obsolete IApplicationLifetime
        private readonly IApplicationLifetime _lifetime;

        private volatile bool _appListening;

        public Worker(ILogger<Worker> logger, IApplicationLifetime lifetime, IServiceProvider serviceProvider)
#pragma warning restore 618
        {
            _logger = logger;
            _lifetime = lifetime;
            _serviceProvider = serviceProvider;
            lifetime.ApplicationStarted.Register(() =>
            {
                _appListening = true;
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!_appListening && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Waiting for app started handling requests");
                await Task.Delay(100, stoppingToken);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                using var serviceScope = _serviceProvider.CreateScope();
                var server = serviceScope.ServiceProvider.GetRequiredService<IServer>();
                var addressFeature = server.Features.Get<IServerAddressesFeature>();
                var address = addressFeature!.Addresses.First();

                var client = new HttpClient();

                _logger.LogInformation("Sending request to self");
                var response = await client.GetAsync($"{address}/api/values", stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error sending request, status code did not indicate success");
                    response.EnsureSuccessStatusCode();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var expected = Program.GetTracerAssemblyLocation();
                if (responseContent != expected)
                {
                    throw new Exception($"Response content '{responseContent}' did not match expected '{expected}");
                }

                _logger.LogInformation("Request sent successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request");
                Program.ExitCode = 1;
            }

            _lifetime.StopApplication();

        }
    }
}
