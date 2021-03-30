#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class PerformanceCountersListener : IRuntimeMetricsListener
    {
        private const string MemoryCategoryName = ".NET CLR Memory";
        private const string ThreadingCategoryName = ".NET CLR LocksAndThreads";

        private readonly IDogStatsd _statsd;
        private readonly PerformanceCounterCategory _memoryCategory;
        private readonly bool _fullInstanceName;
        private readonly string _processName;
        private readonly int _processId;

        private string _instanceName;

        private PerformanceCounterWrapper _gen0Size;
        private PerformanceCounterWrapper _gen1Size;
        private PerformanceCounterWrapper _gen2Size;
        private PerformanceCounterWrapper _lohSize;
        private PerformanceCounterWrapper _contentionCount;

        private int? _previousGen0Count;
        private int? _previousGen1Count;
        private int? _previousGen2Count;

        private double? _lastContentionCount;

        public PerformanceCountersListener(IDogStatsd statsd)
        {
            _statsd = statsd;

            ProcessHelpers.GetCurrentProcessInformation(out _processName, out _, out _processId);

            _memoryCategory = new PerformanceCounterCategory(MemoryCategoryName);

            var instanceName = GetInstanceName();
            _fullInstanceName = instanceName.IsFullName;
            _instanceName = instanceName.Name;

            InitializePerformanceCounters(_instanceName);
        }

        public void Dispose()
        {
            _gen0Size.Dispose();
            _gen1Size.Dispose();
            _gen2Size.Dispose();
            _lohSize.Dispose();
            _contentionCount.Dispose();
        }

        public void Refresh()
        {
            if (!_fullInstanceName)
            {
                _instanceName = GetSimpleInstanceName();
            }

            TryUpdateGauge(MetricsNames.Gen0HeapSize, _gen0Size);
            TryUpdateGauge(MetricsNames.Gen1HeapSize, _gen1Size);
            TryUpdateGauge(MetricsNames.Gen2HeapSize, _gen2Size);
            TryUpdateGauge(MetricsNames.LohSize, _lohSize);

            TryUpdateCounter(MetricsNames.ContentionCount, _contentionCount, ref _lastContentionCount);

            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            if (_previousGen0Count != null)
            {
                _statsd.Increment(MetricsNames.Gen0CollectionsCount, gen0 - _previousGen0Count.Value);
            }

            if (_previousGen1Count != null)
            {
                _statsd.Increment(MetricsNames.Gen1CollectionsCount, gen1 - _previousGen1Count.Value);
            }

            if (_previousGen2Count != null)
            {
                _statsd.Increment(MetricsNames.Gen2CollectionsCount, gen2 - _previousGen2Count.Value);
            }

            _previousGen0Count = gen0;
            _previousGen1Count = gen1;
            _previousGen2Count = gen2;
        }

        private void InitializePerformanceCounters(string instanceName)
        {
            _gen0Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 0 heap size", instanceName);
            _gen1Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 1 heap size", instanceName);
            _gen2Size = new PerformanceCounterWrapper(MemoryCategoryName, "Gen 2 heap size", instanceName);
            _lohSize = new PerformanceCounterWrapper(MemoryCategoryName, "Large Object Heap size", instanceName);
            _contentionCount = new PerformanceCounterWrapper(ThreadingCategoryName, "Total # of Contentions", instanceName);
        }

        private void TryUpdateGauge(string path, PerformanceCounterWrapper counter)
        {
            var value = counter.GetValue(_instanceName);

            if (value != null)
            {
                _statsd.Gauge(path, value.Value);
            }
        }

        private void TryUpdateCounter(string path, PerformanceCounterWrapper counter, ref double? lastValue)
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

            _statsd.Counter(path, value.Value - lastValue.Value);
            lastValue = value;
        }

        private InstanceName GetInstanceName()
        {
            var allInstances = _memoryCategory.GetInstanceNames();
            var fullProcessName = $"{_processName}_p{_processId}_r";

            string tmpName = null;
            foreach (var insName in allInstances)
            {
                if (insName.StartsWith(fullProcessName))
                {
                    // If we have a fullname match we return that
                    return new InstanceName(insName, true);
                }

                if (insName.StartsWith(_processName))
                {
                    if (tmpName is null)
                    {
                        // if is the first relaxed match we store it
                        tmpName = insName;
                    }
                    else
                    {
                        // If we have more than 1 relaxed match we call the GetSimpleInstanceName()
                        return new InstanceName(GetSimpleInstanceName(), false);
                    }
                }
            }

            // if we are here then or we didn't found a relaxed match or we have only 1.
            return new InstanceName(tmpName, false);
        }

        private string GetSimpleInstanceName()
        {
            var allInstanceNames = _memoryCategory.GetInstanceNames();
            int count = 0;
            string tmpInstanceName = null;
            foreach (var insName in allInstanceNames)
            {
                if (!insName.StartsWith(_processName))
                {
                    continue;
                }

                if (count == 0)
                {
                    // First match
                    tmpInstanceName = insName;
                }
                else
                {
                    if (tmpInstanceName is not null)
                    {
                        // first we check the first instance name we found if is not already checked.
                        if (GetInstancePid(tmpInstanceName) == _processId)
                        {
                            return tmpInstanceName;
                        }

                        // If is not the same process id we disable this check for future matches.
                        tmpInstanceName = null;
                    }

                    if (GetInstancePid(insName) == _processId)
                    {
                        return insName;
                    }
                }

                count++;
            }

            return count == 1 ? tmpInstanceName : null;

            static int GetInstancePid(string name)
            {
                using (var counter = new PerformanceCounter(MemoryCategoryName, "Process ID", name, true))
                {
                    return (int)counter.NextValue();
                }
            }
        }

        private readonly struct InstanceName
        {
            public readonly string Name;
            public readonly bool IsFullName;

            public InstanceName(string name, bool isFullName)
            {
                Name = name;
                IsFullName = isFullName;
            }
        }
    }
}
#endif
