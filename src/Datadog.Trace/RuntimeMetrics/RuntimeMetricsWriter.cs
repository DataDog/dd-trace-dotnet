using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.DogStatsd;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeMetricsWriter : IDisposable
    {
        private static readonly string[] GcCountMetricNames = { "runtime.dotnet.gc.count.gen0", "runtime.dotnet.gc.count.gen1", "runtime.dotnet.gc.count.gen2" };
        private static readonly string[] CompactingGcTags = { "compacting_gc:true" };
        private static readonly string[] NotCompactingGcTags = { "compacting_gc:false" };

        private readonly int _delay;

        private readonly IBatchStatsd _statsd;
        private readonly Timer _timer;
        private readonly Process _currentProcess;

#if NETCOREAPP
        private readonly RuntimeEventListener _listener;
#endif

        private readonly Timing _contentionTime = new Timing();

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;

        private long _contentionCount;

        public RuntimeMetricsWriter(IBatchStatsd statsd, int delay)
        {
            _delay = delay;
            _statsd = statsd;
            _timer = new Timer(_ => PushEvents(), null, delay, delay);
            _currentProcess = Process.GetCurrentProcess();
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceException;

            _previousUserCpu = _currentProcess.UserProcessorTime;
            _previousSystemCpu = _currentProcess.PrivilegedProcessorTime;

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
            _currentProcess.Dispose();

#if NETCOREAPP
            _listener.Dispose();
#endif
        }

        private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var name = e.Exception.GetType().Name;

            _statsd.Send(_statsd.GetIncrementCount("runtime.dotnet.exceptions.count", 1, tags: new[] { $"exception_type:{name}" }));
        }

        private void GcPauseTime(TimeSpan timespan)
        {
            _statsd.Send(_statsd.GetSetTiming("runtime.dotnet.gc.pause_time", timespan.TotalMilliseconds));
        }

        private void GcHeapHistory(HeapHistory heapHistory)
        {
            var batch = _statsd.StartBatch();

            if (heapHistory.MemoryLoad != null)
            {
                batch.Append(_statsd.GetSetGauge("runtime.dotnet.gc.memory_load", heapHistory.MemoryLoad.Value));
            }

            batch.Append(_statsd.GetIncrementCount(GcCountMetricNames[heapHistory.Generation], 1, tags: heapHistory.Compacting ? CompactingGcTags : NotCompactingGcTags));

            batch.Send();
        }

        private void GcHeapStats(HeapStats stats)
        {
            var batch = _statsd.StartBatch();

            batch.Append(_statsd.GetSetGauge("runtime.dotnet.gc.size.gen0", stats.Gen0Size));
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.gc.size.gen1", stats.Gen1Size));
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.gc.size.gen2", stats.Gen2Size));
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.gc.size.loh", stats.LohSize));

            batch.Send();
        }

        private void Contention(double durationInNanoseconds)
        {
            _contentionTime.Time(durationInNanoseconds / 1_000_000);
            Interlocked.Increment(ref _contentionCount);
        }

        private void PushEvents()
        {
            _currentProcess.Refresh();

            var newUserCpu = _currentProcess.UserProcessorTime;
            var newSystemCpu = _currentProcess.PrivilegedProcessorTime;

            // Get the CPU time per second
            var userCpu = (newUserCpu - _previousUserCpu).TotalMilliseconds / (_delay / 1000.0);
            var systemCpu = (newSystemCpu - _previousSystemCpu).TotalMilliseconds / (_delay / 1000.0);

            _previousUserCpu = newUserCpu;
            _previousSystemCpu = newSystemCpu;

            var threadCount = _currentProcess.Threads.Count;
            var memoryUsage = _currentProcess.PrivateMemorySize64;

            var batch = _statsd.StartBatch();

            // Can't use a Timing because Dogstatsd doesn't support local aggregation
            // It means that the aggregations in the UI would be wrong
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.threads.contention_time", _contentionTime.Clear()));
            batch.Append(_statsd.GetIncrementCount("runtime.dotnet.threads.contention_count", Interlocked.Exchange(ref _contentionCount, 0)));

            batch.Append(_statsd.GetSetGauge("runtime.dotnet.threads.count", threadCount));

            batch.Append(_statsd.GetSetGauge("runtime.dotnet.mem.committed", memoryUsage));
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.cpu.user", userCpu));
            batch.Append(_statsd.GetSetGauge("runtime.dotnet.cpu.system", systemCpu));

            batch.Send();
        }
    }
}
