using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Datadog.RuntimeMetrics.Hosting
{
    /// <summary>
    /// Wrapper for <see cref="GcMetricsBackgroundService"/> that implements <see cref="IHostedService"/>.
    /// </summary>
    public class GcMetricsHostedService : IHostedService, IDisposable
    {
        private readonly GcMetricsBackgroundService _service;
        private readonly StatsdMetricsSubscriberWrapper _statsd;
        private readonly IDisposable _subscription;

        public GcMetricsHostedService(GcMetricsBackgroundService service, StatsdMetricsSubscriberWrapper statsd)
        {
            _service = service;
            _statsd = statsd;

            _subscription = _service.Subscribe(_statsd);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _service.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _service.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _service?.Dispose();
            _statsd?.Dispose();
        }
    }
}
