// <copyright file="PerformanceCountersListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    // We derive from this type in tests, so need to leave it unsealed
#pragma warning  disable DDSEAL001 // Types should be sealed where possible. Types used for duck-typing cannot be sealed.
    internal class PerformanceCountersListener : IRuntimeMetricsListener
#pragma warning restore DDSEAL001
    {
        private const string MemoryCategoryName = ".NET CLR Memory";
        private const string ThreadingCategoryName = ".NET CLR LocksAndThreads";
        private const string GarbageCollectionMetrics = $"{MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {MetricsNames.ContentionCount}, {MetricsNames.Gen0CollectionsCount}, {MetricsNames.Gen1CollectionsCount}, {MetricsNames.Gen2CollectionsCount}";
        internal const string InsufficientPermissionsMessageTemplate = "The process does not have sufficient permissions to read performance counters. Please refer to https://dtdg.co/net-runtime-metrics to learn how to grant those permissions.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<PerformanceCountersListener>();

        private readonly string _processName;
        private readonly int _processId;

        private readonly IStatsdManager _statsd;

        private string _instanceName;
        private PerformanceCounterCategory _memoryCategory;
        private bool _fullInstanceName;

        private PerformanceCounterWrapper _gen0Size;
        private PerformanceCounterWrapper _gen1Size;
        private PerformanceCounterWrapper _gen2Size;
        private PerformanceCounterWrapper _lohSize;
        private PerformanceCounterWrapper _contentionCount;

        private int? _previousGen0Count;
        private int? _previousGen1Count;
        private int? _previousGen2Count;

        private double? _lastContentionCount;

        private Task _initializationTask;

        public PerformanceCountersListener(IStatsdManager statsd)
        {
            // We assume this is always used by RuntimeMetricsWriter, and therefore we hae already called SetRequired()
            // If it's every used outside that context, we need to update to use SetRequired instead
            _statsd = statsd;

            ProcessHelpers.GetCurrentProcessInformation(out _processName, out _, out _processId);

            // To prevent a potential deadlock when hosted in a service, performance counter initialization must be asynchronous
            // That's because performance counters may rely on wmiApSrv being started,
            // and the windows service manager only allows one service at a time to be starting: https://docs.microsoft.com/en-us/windows/win32/services/service-startup
            _initializationTask = Task.Run(InitializePerformanceCounters);
            _initializationTask.ContinueWith(t => Log.Error(t.Exception, "Error in performance counter initialization task"), TaskContinuationOptions.OnlyOnFaulted);
        }

        public Task WaitForInitialization() => _initializationTask;

        public void Dispose()
        {
            _gen0Size?.Dispose();
            _gen1Size?.Dispose();
            _gen2Size?.Dispose();
            _lohSize?.Dispose();
            _contentionCount?.Dispose();
        }

        public void Refresh()
        {
            if (_initializationTask.Status != TaskStatus.RanToCompletion)
            {
                return;
            }

            if (!_fullInstanceName)
            {
                _instanceName = GetSimpleInstanceName();
            }

            // if we can't send stats (e.g. we're shutting down), there's not much point in
            // running all this, but seeing as we update various state, play it safe and just do no-ops
            using var lease = _statsd.TryGetClientLease();
            var statsd = lease.Client ?? NoOpStatsd.Instance;

            TryUpdateGauge(statsd, MetricsNames.Gen0HeapSize, _gen0Size);
            TryUpdateGauge(statsd, MetricsNames.Gen1HeapSize, _gen1Size);
            TryUpdateGauge(statsd, MetricsNames.Gen2HeapSize, _gen2Size);
            TryUpdateGauge(statsd, MetricsNames.LohSize, _lohSize);

            TryUpdateCounter(statsd, MetricsNames.ContentionCount, _contentionCount, ref _lastContentionCount);

            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            if (_previousGen0Count != null)
            {
                statsd.Increment(MetricsNames.Gen0CollectionsCount, gen0 - _previousGen0Count.Value);
            }

            if (_previousGen1Count != null)
            {
                statsd.Increment(MetricsNames.Gen1CollectionsCount, gen1 - _previousGen1Count.Value);
            }

            if (_previousGen2Count != null)
            {
                statsd.Increment(MetricsNames.Gen2CollectionsCount, gen2 - _previousGen2Count.Value);
            }

            _previousGen0Count = gen0;
            _previousGen1Count = gen1;
            _previousGen2Count = gen2;

            if (statsd is not NoOpStatsd)
            {
                Log.Debug("Sent the following metrics to the DD agent: {Metrics}", GarbageCollectionMetrics);
            }
        }

        protected virtual void InitializePerformanceCounters()
        {
            try
            {
                _memoryCategory = new PerformanceCounterCategory(MemoryCategoryName);

                var instanceName = GetInstanceName();
                _fullInstanceName = instanceName.Item2;
                _instanceName = instanceName.Item1;

                _gen0Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 0 heap size", _instanceName);
                _gen1Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 1 heap size", _instanceName);
                _gen2Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 2 heap size", _instanceName);
                _lohSize = new PerformanceCounterWrapper(MemoryCategoryName, "Large Object Heap size", _instanceName);
                _contentionCount = new PerformanceCounterWrapper(ThreadingCategoryName, "Total # of Contentions", _instanceName);
            }
            catch (UnauthorizedAccessException ex) when (ex.Message.Contains("'Global'"))
            {
                // Catching error UnauthorizedAccessException: Access to the registry key 'Global' is denied.
                // The 'Global' part seems consistent across localizations

                Log.ErrorSkipTelemetry(ex, InsufficientPermissionsMessageTemplate);
                throw;
            }
            catch (Exception ex)
            {
                Log.ErrorSkipTelemetry(ex, "An error occured while initializing the performance counters");
                throw;
            }
        }

        private void TryUpdateGauge(IDogStatsd statsd, string path, PerformanceCounterWrapper counter)
        {
            var value = counter.GetValue(_instanceName);

            if (value != null)
            {
                statsd.Gauge(path, value.Value);
            }
        }

        private void TryUpdateCounter(IDogStatsd statsd, string path, PerformanceCounterWrapper counter, ref double? lastValue)
        {
            var value = counter.GetValue(_instanceName);

            if (value == null)
            {
                return;
            }

            if (lastValue == null)
            {
                lastValue = value;
                return;
            }

            statsd.Counter(path, value.Value - lastValue.Value);
            lastValue = value;
        }

        private Tuple<string, bool> GetInstanceName()
        {
            var instanceNames = _memoryCategory.GetInstanceNames().Where(n => n.StartsWith(_processName)).ToArray();

            // The instance can contain the pid, which will avoid looking through multiple processes that would have the same name
            // See https://docs.microsoft.com/en-us/dotnet/framework/debug-trace-profile/performance-counters-and-in-process-side-by-side-applications#performance-counters-for-in-process-side-by-side-applications
            var fullName = instanceNames.FirstOrDefault(n => n.StartsWith($"{_processName}_p{_processId}_r"));

            if (fullName != null)
            {
                return Tuple.Create(fullName, true);
            }

            if (instanceNames.Length == 1)
            {
                return Tuple.Create(instanceNames[0], false);
            }

            return Tuple.Create(GetSimpleInstanceName(), false);
        }

        private string GetSimpleInstanceName()
        {
            var instanceNames = _memoryCategory.GetInstanceNames().Where(n => n.StartsWith(_processName)).ToArray();

            if (instanceNames.Length == 1)
            {
                return instanceNames[0];
            }

            foreach (var name in instanceNames)
            {
                int instancePid;

                using (var counter = new PerformanceCounter(MemoryCategoryName, "Process ID", name, true))
                {
                    instancePid = (int)counter.NextValue();
                }

                if (instancePid == _processId)
                {
                    return name;
                }
            }

            return null;
        }
    }
}
#endif
