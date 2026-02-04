using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ocelot.Configuration;
using Ocelot.Configuration.Creator;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

                // Update Ocelot's internal configuration to point the downstream route back to this application
                await UpdateOcelotInternalConfig(serviceScope.ServiceProvider, address);

                // Send a request through the proxy
                using var client = new HttpClient();

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

        private async Task UpdateOcelotInternalConfig(IServiceProvider serviceProvider, string address)
        {
            var uri = new Uri(address);

            // Create the file configuration with the correct downstream address
            var fileConfig = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new FileRoute
                    {
                        DownstreamPathTemplate = "/",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new FileHostAndPort
                            {
                                Host = uri.Host,
                                Port = uri.Port
                            }
                        },
                        UpstreamPathTemplate = "/proxy",
                        UpstreamHttpMethod = new List<string> { "Get" }
                    }
                },
                GlobalConfiguration = new FileGlobalConfiguration
                {
                    BaseUrl = address
                }
            };

            // Get Ocelot's internal configuration creator and repository
            var configCreator = serviceProvider.GetRequiredService<IInternalConfigurationCreator>();
            var configRepo = serviceProvider.GetRequiredService<IInternalConfigurationRepository>();

            // Create the internal configuration from file configuration
            var response = await configCreator.Create(fileConfig);
            if (response.IsError)
            {
                _logger.LogError("Failed to create Ocelot configuration: {Errors}", string.Join(", ", response.Errors.Select(e => e.Message)));
                throw new Exception($"Failed to create Ocelot configuration: {string.Join(", ", response.Errors.Select(e => e.Message))}");
            }

            // Update the internal configuration repository
            var addOrReplaceResponse = configRepo.AddOrReplace(response.Data);
            if (addOrReplaceResponse.IsError)
            {
                _logger.LogError("Failed to update Ocelot configuration: {Errors}", string.Join(", ", addOrReplaceResponse.Errors.Select(e => e.Message)));
                throw new Exception($"Failed to update Ocelot configuration: {string.Join(", ", addOrReplaceResponse.Errors.Select(e => e.Message))}");
            }

            _logger.LogInformation("Updated Ocelot internal configuration with address: {Address}", address);
        }
    }
}
