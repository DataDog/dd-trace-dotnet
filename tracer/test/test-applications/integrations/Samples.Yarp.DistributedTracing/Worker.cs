using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Samples.Yarp.DistributedTracing
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IProxyConfigProvider _proxyConfigProvider;

#pragma warning disable 618 // ignore obsolete IApplicationLifetime
        private readonly IApplicationLifetime _lifetime;

        private volatile bool _appListening;

        public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, IProxyConfigProvider proxyConfigProvider, IApplicationLifetime lifetime)
#pragma warning restore 618
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _proxyConfigProvider = proxyConfigProvider;
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

                // First, update the proxy so the destination points back to this application
                if (_proxyConfigProvider is CodeProxyConfigProvider codeProvider)
                {
                    codeProvider.Update(address);
                };

                // Next, send a request. Use a SocketsHttpHandler with ActivityHeadersPropagator = null
                // to prevent the OTel SDK's DiagnosticsHandler from injecting a foreign traceparent
                // header that would conflict with Datadog's trace context on the inbound request.
                // This is a test-only workaround — in production, the external caller originates
                // outside the process and is unaffected by the in-process OTel SDK.
#if NET6_0_OR_GREATER
                var handler = new SocketsHttpHandler { ActivityHeadersPropagator = null };
                using var client = new HttpClient(handler);
#else
                var client = new HttpClient();
#endif

                _logger.LogInformation("Sending request to self");
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
                await Task.Delay(500);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request");
            }

            _lifetime.StopApplication();
        }
    }
}
