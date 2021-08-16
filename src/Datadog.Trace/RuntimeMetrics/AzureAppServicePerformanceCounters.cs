// <copyright file="AzureAppServicePerformanceCounters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class AzureAppServicePerformanceCounters : IRuntimeMetricsListener
    {
        internal const string EnvironmentVariableName = "WEBSITE_COUNTERS_CLR";

        private readonly IDogStatsd _statsd;

        private int? _previousGen0Count;
        private int? _previousGen1Count;
        private int? _previousGen2Count;

        public AzureAppServicePerformanceCounters(IDogStatsd statsd)
        {
            _statsd = statsd;
        }

        public void Dispose()
        {
        }

        public void Refresh()
        {
            var rawValue = EnvironmentHelpers.GetEnvironmentVariable(EnvironmentVariableName);
            var value = JsonConvert.DeserializeObject<PerformanceCountersValue>(rawValue);

            _statsd.Gauge(MetricsNames.Gen0HeapSize, value.Gen0Size);
            _statsd.Gauge(MetricsNames.Gen1HeapSize, value.Gen1Size);
            _statsd.Gauge(MetricsNames.Gen2HeapSize, value.Gen2Size);
            _statsd.Gauge(MetricsNames.LohSize, value.LohSize);

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

        private class PerformanceCountersValue
        {
            [JsonProperty("gen0HeapSize")]
            public int Gen0Size { get; set; }

            [JsonProperty("gen1HeapSize")]
            public int Gen1Size { get; set; }

            [JsonProperty("gen2HeapSize")]
            public int Gen2Size { get; set; }

            [JsonProperty("largeObjectHeapSize")]
            public int LohSize { get; set; }
        }
    }
}

#endif
