// <copyright file="DiagnosticsMetricsRuntimeMetricsListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RuntimeMetrics;

internal sealed class DiagnosticsMetricsRuntimeMetricsListener : IRuntimeMetricsListener
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticsMetricsRuntimeMetricsListener>();

    // Note that we don't currently record Pinned Object Heap sizes
    private static readonly string[] GcGenSizeMetricNames = [MetricsNames.Gen0HeapSize, MetricsNames.Gen1HeapSize, MetricsNames.Gen2HeapSize, MetricsNames.LohSize];
    private static readonly string[] GcGenCountMetricNames = [MetricsNames.Gen0CollectionsCount, MetricsNames.Gen1CollectionsCount, MetricsNames.Gen2CollectionsCount];

    private static readonly int MaxGcSizeGenerations = Math.Min(GC.GetGCMemoryInfo().GenerationInfo.Length, GcGenSizeMetricNames.Length);
    private static readonly int MaxGcCountGeneration = Math.Min(GC.MaxGeneration, GcGenCountMetricNames.Length - 1);

    private readonly IStatsdManager _statsd;
    private readonly MeterListener _listener;
    private readonly bool _aspnetcoreMetricsAvailable;
    private readonly long?[] _previousGenCounts = [null, null, null];
    private readonly Func<DiagnosticsMetricsRuntimeMetricsListener, double?> _getGcPauseTimeFunc;

    private double _gcPauseTimeSeconds;

    private long _activeRequests;
    private long _failedRequests;
    private long _successRequests;
    private long _queuedRequests;
    private long _activeConnections;
    private long _queuedConnections;
    private long _totalClosedConnections;

    private double? _previousGcPauseTime;

    private long? _previousContentionCount;

    public DiagnosticsMetricsRuntimeMetricsListener(IStatsdManager statsd)
    {
        _statsd = statsd;
        _listener = new()
        {
            InstrumentPublished = OnInstrumentPublished,
        };

        // ASP.NET Core metrics are only available on .NET 8+
        _aspnetcoreMetricsAvailable = Environment.Version.Major >= 8;

        if (Environment.Version.Major >= 9)
        {
            // System.Runtime metrics are only available on .NET 9+, but the only one we need it for is GC pause time
            _getGcPauseTimeFunc = GetGcPauseTime_RuntimeMetrics;
        }
        else if (Environment.Version.Major > 6
                 || Environment.Version is { Major: 6, Build: >= 21 })
        {
            // .NET 6.0.21 introduced the GC.GetTotalPauseDuration() method https://github.com/dotnet/runtime/pull/87143
            // Which is what OTel uses where required: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/5aa6d868/src/OpenTelemetry.Instrumentation.Runtime/RuntimeMetrics.cs#L105C40-L107
            // We could use ducktyping instead of reflection, but this is such a simple case that it's kind of easier
            // to just go with the delegate approach
            var methodInfo = typeof(GC).GetMethod("GetTotalPauseDuration", BindingFlags.Public | BindingFlags.Static);
            if (methodInfo is null)
            {
                // strange, but we failed to get the delegate
                _getGcPauseTimeFunc = GetGcPauseTime_Noop;
            }
            else
            {
                var getTotalPauseDuration = methodInfo.CreateDelegate<Func<TimeSpan>>();
                _getGcPauseTimeFunc = _ => getTotalPauseDuration().TotalMilliseconds;
            }
        }
        else
        {
            // can't get pause time
            _getGcPauseTimeFunc = GetGcPauseTime_Noop;
        }

        // The .NET runtime instruments we listen to only produce long or double values
        // so that's all we listen for here
        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecordedLong);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecordedDouble);

        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    public void Refresh()
    {
        // This triggers the observable metrics to go and read the values, then calls the OnMeasurement values to send them to us
        _listener.RecordObservableInstruments();

        // Now we calculate and send the values to statsd.
        using var lease = _statsd.TryGetClientLease();
        var statsd = lease.Client ?? NoOpStatsd.Instance;

        // There are many stats that we can grab directly, without needing to use the metrics APIs (which just wrap these calls anyway)
        statsd.Gauge(MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);
        Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.ThreadPoolWorkersCount}");

        var contentionCount = Monitor.LockContentionCount;
        if (_previousContentionCount.HasValue)
        {
            statsd.Counter(MetricsNames.ContentionCount, contentionCount - _previousContentionCount.Value);
            Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.ContentionCount}");
        }

        _previousContentionCount = contentionCount;

        // GC Heap Size based on "dotnet.gc.last_collection.heap.size" metric
        // from https://github.com/dotnet/runtime/blob/v10.0.1/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/RuntimeMetrics.cs#L185
        // If we call this API before _any_ GCs happen, we'll get 0s for the heap size, so check for that and bail skip emitting if so
        var gcInfo = GC.GetGCMemoryInfo();
        if (gcInfo.Index != 0)
        {
            // Heap sizes
            for (var i = 0; i < MaxGcSizeGenerations; ++i)
            {
                statsd.Gauge(GcGenSizeMetricNames[i], gcInfo.GenerationInfo[i].SizeAfterBytes);
            }

            // memory load
            // This is attempting to emulate the GcGlobalHeapHistory.MemoryLoad event details
            // That value is calculated using
            // - `current_gc_data_global->mem_pressure` (src/coreclr/gc/gc.cpp#L3288)
            // - which fetches the value set via `history->mem_pressure = entry_memory_load` (src/coreclr/gc/gc.cpp#L7912)
            // - which is set by calling `gc_heap::get_memory_info()` (src/coreclr/gc/gc.cpp#L29438)
            // - which then calls GCToOSInterface::GetMemoryStatus(...) which has platform-specific implementations
            // - On linux, memory_load is calculated differently depending if there's a restriction (src/coreclr/gc/unix/gcenv.unix.cpp#L1191)
            //   - Physical Memory Used / Limit
            //   - (g_totalPhysicalMemSize - GetAvailablePhysicalMemory()) / total
            // - On Windows, memory_load is calculated differently depending if there's a restriction (src/coreclr/gc/unix/gcenv.windows.cpp#L1000)
            //   - Working Set Size / Limit
            //   - GlobalMemoryStatusEx -> (ullTotalVirtual - ullAvailVirtual) * 100.0 / (float)ms.ullTotalVirtual
            //
            // We try to roughly emulate that using the info in gcInfo:
            var availableBytes = gcInfo.TotalAvailableMemoryBytes;

            if (availableBytes > 0)
            {
                // This can return a value > 1 (for values I don't _entirely_ understand), so clamp it to 1.0
                statsd.Gauge(MetricsNames.GcMemoryLoad, (double)gcInfo.MemoryLoadBytes * 100.0 / availableBytes);
            }
        }
        else
        {
            Log.Debug("No GC collections yet, skipping heap size metrics");
        }

        var gcPauseTimeMilliSeconds = _getGcPauseTimeFunc(this);
        // We don't record 0-length pauses, so that we match RuntimeEventListener behaviour
        // We don't worry about the floating point comparison, as reporting close to zero is fine
        if (gcPauseTimeMilliSeconds.HasValue && _previousGcPauseTime.HasValue
                                             && gcPauseTimeMilliSeconds.Value != _previousGcPauseTime.Value)
        {
            statsd.Timer(MetricsNames.GcPauseTime, gcPauseTimeMilliSeconds.Value - _previousGcPauseTime.Value);
        }

        _previousGcPauseTime = gcPauseTimeMilliSeconds;

        // GC Collection counts based on "dotnet.gc.collections" metric
        // from https://github.com/dotnet/runtime/blob/v10.0.1/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/RuntimeMetrics.cs#L159
        long collectionsFromHigherGeneration = 0;

        for (var gen = MaxGcCountGeneration; gen >= 0; --gen)
        {
            long collectionsFromThisGeneration = GC.CollectionCount(gen);
            var thisCount = collectionsFromThisGeneration - collectionsFromHigherGeneration;
            collectionsFromHigherGeneration = collectionsFromThisGeneration;

            var previous = _previousGenCounts[gen];
            _previousGenCounts[gen] = thisCount;

            if (previous.HasValue)
            {
                var increment = (int)Math.Min(thisCount - previous.Value, int.MaxValue);
                // don't need to report zero increments
                if (increment != 0)
                {
                    statsd.Increment(GcGenCountMetricNames[gen], increment);
                }
            }
        }

        // This isn't strictly true, due to the "previous counts" behavior, but it's good enough, and what we in other listeners
        Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {MetricsNames.ContentionCount}, {MetricsNames.Gen0CollectionsCount}, {MetricsNames.Gen1CollectionsCount}, {MetricsNames.Gen2CollectionsCount}, {MetricsNames.GcPauseTime}, {MetricsNames.GcMemoryLoad}");

        // aspnetcore metrics
        if (_aspnetcoreMetricsAvailable)
        {
            var activeRequests = Interlocked.Read(ref _activeRequests);
            var failedRequests = Interlocked.Read(ref _failedRequests);
            var successRequests = Interlocked.Read(ref _successRequests);
            var queuedRequests = Interlocked.Read(ref _queuedRequests);
            var currentConnections = Interlocked.Read(ref _activeConnections);
            var queuedConnections = Interlocked.Read(ref _queuedConnections);
            var totalClosedConnections = Interlocked.Read(ref _totalClosedConnections);

            statsd.Gauge(MetricsNames.AspNetCoreCurrentRequests, activeRequests);
            // Recording these as never-reset gauges seems a bit strange to me as it could easily overflow
            // but it's what the event listener already does, so I guess it's required (changing it would be problematic I think)
            statsd.Gauge(MetricsNames.AspNetCoreFailedRequests, failedRequests);
            statsd.Gauge(MetricsNames.AspNetCoreTotalRequests, failedRequests + successRequests);
            statsd.Gauge(MetricsNames.AspNetCoreRequestQueueLength, queuedRequests);

            statsd.Gauge(MetricsNames.AspNetCoreCurrentConnections, currentConnections);
            statsd.Gauge(MetricsNames.AspNetCoreConnectionQueueLength, queuedConnections);

            // Same here, seems risky to have this as a gauge, but I think that ship has sailed
            // Note also that as _totalClosedConnections doesn't include _current_ connections, we add that in
            statsd.Gauge(MetricsNames.AspNetCoreTotalConnections, totalClosedConnections + currentConnections);
            Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.AspNetCoreCurrentRequests}, {MetricsNames.AspNetCoreFailedRequests}, {MetricsNames.AspNetCoreTotalRequests}, {MetricsNames.AspNetCoreRequestQueueLength}, {MetricsNames.AspNetCoreCurrentConnections}, {MetricsNames.AspNetCoreConnectionQueueLength}, {MetricsNames.AspNetCoreTotalConnections}");
        }
    }

    private static void OnMeasurementRecordedDouble(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var handler = (DiagnosticsMetricsRuntimeMetricsListener)state!;
        switch (instrument.Name)
        {
            case "dotnet.gc.pause.time":
                Interlocked.Exchange(ref handler._gcPauseTimeSeconds, measurement);
                break;
            case "kestrel.connection.duration":
                Interlocked.Increment(ref handler._totalClosedConnections);
                break;
            case "http.server.request.duration":
                foreach (var tagPair in tags)
                {
                    if (tagPair is { Key: "http.response.status_code" })
                    {
                        if (tagPair.Value is >= 500)
                        {
                            Interlocked.Increment(ref handler._failedRequests);
                        }
                        else
                        {
                            Interlocked.Increment(ref handler._successRequests);
                        }

                        return;
                    }
                }

                break;
        }
    }

    private static void OnMeasurementRecordedLong(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var handler = (DiagnosticsMetricsRuntimeMetricsListener)state!;
        switch (instrument.Name)
        {
            // Ignore tags for these up-down counters, we only care about totals
            case "http.server.active_requests":
                Interlocked.Add(ref handler._activeRequests, measurement);
                break;

            case "kestrel.active_connections":
                Interlocked.Add(ref handler._activeConnections, measurement);
                break;

            case "kestrel.queued_connections":
                Interlocked.Add(ref handler._queuedConnections, measurement);
                break;

            case "kestrel.queued_requests":
                Interlocked.Add(ref handler._queuedRequests, measurement);
                break;
        }
    }

    private static double? GetGcPauseTime_RuntimeMetrics(DiagnosticsMetricsRuntimeMetricsListener listener)
    {
        var gcPauseTimeSeconds = Interlocked.Exchange(ref listener._gcPauseTimeSeconds, 0);
        return gcPauseTimeSeconds * 1_000;
    }

    private static double? GetGcPauseTime_Noop(DiagnosticsMetricsRuntimeMetricsListener listener) => null;

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        // We want the following Meter/instruments:
        //
        // System.Runtime
        // - dotnet.gc.pause.time: MetricsNames.GcPauseTime (where possible)
        // - [dotnet.gc.collections (tagged by gc.heap.generation=gen0)] - we get these via built-in APIs which are functionally identical
        // - [dotnet.gc.last_collection.heap.size (gc.heap.generation=gen0/gen1/gen2/loh/poh)]  - we get these via built-in APIs which are functionally identical
        //
        // Microsoft.AspNetCore.Hosting
        // - http.server.active_requests: MetricsNames.AspNetCoreCurrentRequests
        // - http.server.request.duration: MetricsNames.AspNetCoreTotalRequests, MetricsNames.AspNetCoreFailedRequests
        //
        // Microsoft.AspNetCore.Server.Kestrel
        // - kestrel.active_connections: MetricsNames.AspNetCoreCurrentConnections,
        // - kestrel.queued_connections: MetricsNames.AspNetCoreConnectionQueueLength,
        // - kestrel.connection.duration: MetricsNames.AspNetCoreTotalConnections,
        // - kestrel.queued_requests: MetricsNames.AspNetCoreRequestQueueLength
        //
        // We have no way to get these:
        // - MetricsNames.ContentionTime. Only available using EventListener
        var meterName = instrument.Meter.Name;
        var instrumentName = instrument.Name;
        if ((string.Equals(meterName, "System.Runtime", StringComparison.Ordinal) && instrumentName is "dotnet.gc.pause.time")
           || (string.Equals(meterName, "Microsoft.AspNetCore.Hosting", StringComparison.Ordinal) && instrumentName is
                  "http.server.active_requests" or
                  "http.server.request.duration")
          || (string.Equals(meterName, "Microsoft.AspNetCore.Server.Kestrel", StringComparison.Ordinal) && instrumentName is
                  "kestrel.active_connections" or
                  "kestrel.queued_connections" or
                  "kestrel.connection.duration" or
                  "kestrel.queued_requests"))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Enabled measurement events for instrument: {MeterName}/{InstrumentName} ", meterName, instrumentName);
            }

            listener.EnableMeasurementEvents(instrument, state: this);
        }
    }
}

#endif
