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
        private static readonly string[] GcCountMetricNames = { "runtime.dotnet.gc.count.gen0", "runtime.dotnet.gc.count.gen1", "runtime.dotnet.gc.count.gen2" };
        private static readonly string[] CompactingGcTags = { "compacting_gc:true" };
        private static readonly string[] NotCompactingGcTags = { "compacting_gc:false" };

        private readonly int _delay;

        private readonly IDogStatsd _statsd;
        private readonly Timer _timer;

#if NETCOREAPP
        private readonly RuntimeEventListener _listener;
#endif

        private readonly Timing _contentionTime = new Timing();
        private readonly bool _enableProcessMetrics;

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;

        private long _contentionCount;

        public RuntimeMetricsWriter(IDogStatsd statsd, int delay)
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
#if NETCOREAPP
            _listener = new RuntimeEventListener();
            _listener.GcHeapStats += GcHeapStats;
            _listener.GcPauseTime += GcPauseTime;
            _listener.GcHeapHistory += GcHeapHistory;
            _listener.Contention += Contention;
#endif
        }

        public void Dispose()
        {
            _timer.Dispose();

#if NETCOREAPP
            _listener.Dispose();
#endif
        }

        private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var name = e.Exception.GetType().Name;

            _statsd.Increment("runtime.dotnet.exceptions.count", 1, tags: new[] { $"exception_type:{name}" });
        }

        private void GcPauseTime(TimeSpan timespan)
        {
            _statsd.Timer("runtime.dotnet.gc.pause_time", timespan.TotalMilliseconds);
        }

        private void GcHeapHistory(HeapHistory heapHistory)
        {
            if (heapHistory.MemoryLoad != null)
            {
                _statsd.Gauge("runtime.dotnet.gc.memory_load", heapHistory.MemoryLoad.Value);
            }

            _statsd.Increment(GcCountMetricNames[heapHistory.Generation], 1, tags: heapHistory.Compacting ? CompactingGcTags : NotCompactingGcTags);
        }

        private void GcHeapStats(HeapStats stats)
        {
            _statsd.Gauge("runtime.dotnet.gc.size.gen0", stats.Gen0Size);
            _statsd.Gauge("runtime.dotnet.gc.size.gen1", stats.Gen1Size);
            _statsd.Gauge("runtime.dotnet.gc.size.gen2", stats.Gen2Size);
            _statsd.Gauge("runtime.dotnet.gc.size.loh", stats.LohSize);
        }

        private void Contention(double durationInNanoseconds)
        {
            _contentionTime.Time(durationInNanoseconds / 1_000_000);
            Interlocked.Increment(ref _contentionCount);
        }

        private void PushEvents()
        {
            try
            {
                if (_enableProcessMetrics)
                {
                    ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var newUserCpu, out var newSystemCpu, out var threadCount, out var memoryUsage);

                    // Get the CPU time per second
                    var userCpu = (newUserCpu - _previousUserCpu).TotalMilliseconds / (_delay / 1000.0);
                    var systemCpu = (newSystemCpu - _previousSystemCpu).TotalMilliseconds / (_delay / 1000.0);

                    _previousUserCpu = newUserCpu;
                    _previousSystemCpu = newSystemCpu;

                    _statsd.Gauge("runtime.dotnet.threads.count", threadCount);

                    _statsd.Gauge("runtime.dotnet.mem.committed", memoryUsage);
                    _statsd.Gauge("runtime.dotnet.cpu.user", userCpu);
                    _statsd.Gauge("runtime.dotnet.cpu.system", systemCpu);
                }

                // Can't use a Timing because Dogstatsd doesn't support local aggregation
                // It means that the aggregations in the UI would be wrong
                _statsd.Gauge("runtime.dotnet.threads.contention_time", _contentionTime.Clear());
                _statsd.Counter("runtime.dotnet.threads.contention_count", Interlocked.Exchange(ref _contentionCount, 0));

#if NETCOREAPP
            _statsd.Gauge("runtime.dotnet.threads.workers_count", ThreadPool.ThreadCount);
#endif
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while updating runtime metrics");
            }
        }
    }
}
