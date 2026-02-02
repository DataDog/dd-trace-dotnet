using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Ocelot.DistributedTracing
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;

#pragma warning disable 618 // ignore obsolete IApplicationLifetime
        private readonly IApplicationLifetime _lifetime;

        private volatile bool _appListening;

        public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, IApplicationLifetime lifetime)
#pragma warning restore 618
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
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

                // Update ocelot.json to point the downstream route back to this application
                await UpdateOcelotConfig(address);

                // Give Ocelot a moment to pick up the config change
                await Task.Delay(500, stoppingToken);

                // Send a request through the proxy
                var client = new HttpClient();

                _logger.LogInformation("Sending request to self via Ocelot proxy");
                var response = await client.GetAsync($"{address}/proxy", stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error sending request, status code did not indicate success");
                    response.EnsureSuccessStatusCode();
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Request sent successfully");
                _logger.LogInformation("Response: {Body}", responseContent);

                // Wait for 500ms to see if it helps with the CI testing
                await Task.Delay(500, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request");
            }

            _lifetime.StopApplication();
        }

        private async Task UpdateOcelotConfig(string address)
        {
            // Update ocelot.json with the actual address
            var ocelotConfig = new
            {
                Routes = new[]
                {
                    new
                    {
                        DownstreamPathTemplate = "/",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new[]
                        {
                            new
                            {
                                Host = new Uri(address).Host,
                                Port = new Uri(address).Port
                            }
                        },
                        UpstreamPathTemplate = "/proxy",
                        UpstreamHttpMethod = new[] { "Get" }
                    }
                },
                GlobalConfiguration = new
                {
                    BaseUrl = address
                }
            };

            var json = JsonSerializer.Serialize(ocelotConfig, new JsonSerializerOptions { WriteIndented = true });
            var configPath = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
            await File.WriteAllTextAsync(configPath, json);

            _logger.LogInformation("Updated ocelot.json with address: {Address}", address);
        }
    }
}
