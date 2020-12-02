using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeMetricsWriter : IDisposable
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RuntimeMetricsWriter>();

        private readonly int _delay;

        private readonly IDogStatsd _statsd;
        private readonly Timer _timer;

        private readonly IRuntimeMetricsListener _listener;

        private readonly bool _enableProcessMetrics;

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;

        public RuntimeMetricsWriter(IDogStatsd statsd, int delay)
            : this(statsd, delay, InitializeListener)
        {
        }

        internal RuntimeMetricsWriter(IDogStatsd statsd, int delay, Func<IDogStatsd, IRuntimeMetricsListener> initializeListener)
        {
            _delay = delay;
            _statsd = statsd;
            _timer = new Timer(_ => PushEvents(), null, delay, delay);

            try
            {
                AppDomain.CurrentDomain.FirstChanceException += FirstChanceException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "First chance exceptions won't be monitored");
            }

            try
            {
                ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var userCpu, out var systemCpu, out _, out _);

                _previousUserCpu = userCpu;
                _previousSystemCpu = systemCpu;

                _enableProcessMetrics = true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to get current process information");
                _enableProcessMetrics = false;
            }

            try
            {
                _listener = initializeListener(statsd);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to initialize runtime listener, some runtime metrics will be missing");
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= FirstChanceException;
            _timer.Dispose();
            _listener?.Dispose();
        }

        private static IRuntimeMetricsListener InitializeListener(IDogStatsd statsd)
        {
#if NETCOREAPP
            return new RuntimeEventListener(statsd);
#elif NETFRAMEWORK
            return new PerformanceCountersListener(statsd);
#else
            return null;
#endif
        }

        private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var name = e.Exception.GetType().Name;

            _statsd.Increment(MetricsNames.ExceptionsCount, 1, tags: new[] { $"exception_type:{name}" });
        }

        private void PushEvents()
        {
            try
            {
                _listener?.Refresh();

                if (_enableProcessMetrics)
                {
                    ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var newUserCpu, out var newSystemCpu, out var threadCount, out var memoryUsage);

                    // Get the CPU time per second
                    var userCpu = (newUserCpu - _previousUserCpu).TotalMilliseconds / (_delay / 1000.0);
                    var systemCpu = (newSystemCpu - _previousSystemCpu).TotalMilliseconds / (_delay / 1000.0);

                    _previousUserCpu = newUserCpu;
                    _previousSystemCpu = newSystemCpu;

                    _statsd.Gauge(MetricsNames.ThreadsCount, threadCount);

                    _statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
                    _statsd.Gauge(MetricsNames.CpuUserTime, userCpu);
                    _statsd.Gauge(MetricsNames.CpuSystemTime, systemCpu);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while updating runtime metrics");
            }
        }
    }
}
