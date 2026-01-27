// <copyright file="AzureAppServicePerformanceCounters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal sealed class AzureAppServicePerformanceCounters : IRuntimeMetricsListener
    {
        private const string GarbageCollectionMetrics = $"{MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {MetricsNames.Gen0CollectionsCount}, {MetricsNames.Gen1CollectionsCount}, {MetricsNames.Gen2CollectionsCount}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureAppServicePerformanceCounters>();
        private readonly IStatsdManager _statsd;

        private int? _previousGen0Count;
        private int? _previousGen1Count;
        private int? _previousGen2Count;

        public AzureAppServicePerformanceCounters(IStatsdManager statsd)
        {
            // We assume this is always used by RuntimeMetricsWriter, and therefore we hae already called SetRequired()
            // If it's every used outside that context, we need to update to use SetRequired instead
            _statsd = statsd;
        }

        public void Dispose()
        {
        }

        public void Refresh()
        {
            using var lease = _statsd.TryGetClientLease();
            if (lease.Client is not { } statsd)
            {
                // bail out, we have no client for some reason
                return;
            }

            var rawValue = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.AzureAppService.CountersKey);
            var value = JsonConvert.DeserializeObject<PerformanceCountersValue>(rawValue);

            statsd.Gauge(MetricsNames.Gen0HeapSize, value.Gen0Size);
            statsd.Gauge(MetricsNames.Gen1HeapSize, value.Gen1Size);
            statsd.Gauge(MetricsNames.Gen2HeapSize, value.Gen2Size);
            statsd.Gauge(MetricsNames.LohSize, value.LohSize);

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

            Log.Debug("Sent the following metrics to the DD agent: {Metrics}", GarbageCollectionMetrics);
        }

        private sealed class PerformanceCountersValue
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
