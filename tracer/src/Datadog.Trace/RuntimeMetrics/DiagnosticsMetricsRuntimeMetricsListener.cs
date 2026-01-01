// <copyright file="DiagnosticsMetricsRuntimeMetricsListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
    private readonly bool _systemRuntimeMetricsAvailable;
    private readonly bool _aspnetcoreMetricsAvailable;
    private readonly long?[] _previousGenCounts = [null, null, null];

    private double _gcPauseTimeSeconds;

    private long _activeRequests;
    private long _failedRequests;
    private long _successRequests;
    private long _queuedRequests;
    private long _activeConnections;
    private long _queuedConnections;
    private long _totalConnections;

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

        // System.Runtime metrics are only available on .NET 9+
        _systemRuntimeMetricsAvailable = Environment.Version.Major >= 9;

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

        // now we send the values to statsd.
        // This avoids taking a lease for each individual measurement, which keeps that part fast, like it should be
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
            for (var i = 0; i < MaxGcSizeGenerations; ++i)
            {
                statsd.Gauge(GcGenSizeMetricNames[i], gcInfo.GenerationInfo[i].SizeAfterBytes);
            }
        }
        else
        {
            Log.Debug("No GC collections yet, skipping heap size metrics");
        }

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
                statsd.Increment(GcGenCountMetricNames[gen], increment);
            }
        }

        // This isn't strictly true, due to the "previous counts" behavior, but it's good enough, and what we in other listeners
        Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {MetricsNames.ContentionCount}, {MetricsNames.Gen0CollectionsCount}, {MetricsNames.Gen1CollectionsCount}, {MetricsNames.Gen2CollectionsCount}");

        // aspnetcore metrics
        if (_aspnetcoreMetricsAvailable)
        {
            statsd.Gauge(MetricsNames.AspNetCoreCurrentRequests, Interlocked.Read(ref _activeRequests));
            var failed = Interlocked.Exchange(ref _failedRequests, 0);
            statsd.Gauge(MetricsNames.AspNetCoreFailedRequests, failed);
            statsd.Gauge(MetricsNames.AspNetCoreTotalRequests, failed + Interlocked.Exchange(ref _successRequests, 0));
            statsd.Gauge(MetricsNames.AspNetCoreRequestQueueLength, Interlocked.Read(ref _queuedRequests));
            statsd.Gauge(MetricsNames.AspNetCoreCurrentConnections, Interlocked.Read(ref _activeConnections));
            statsd.Gauge(MetricsNames.AspNetCoreConnectionQueueLength, Interlocked.Read(ref _queuedConnections));
            statsd.Gauge(MetricsNames.AspNetCoreTotalConnections, Interlocked.Exchange(ref _totalConnections, 0));
            Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.AspNetCoreCurrentRequests}, {MetricsNames.AspNetCoreFailedRequests}, {MetricsNames.AspNetCoreTotalRequests}, {MetricsNames.AspNetCoreRequestQueueLength}, {MetricsNames.AspNetCoreCurrentConnections}, {MetricsNames.AspNetCoreConnectionQueueLength}, {MetricsNames.AspNetCoreTotalConnections}");
        }

        if (_systemRuntimeMetricsAvailable)
        {
            var gcPauseTimeSeconds = Interlocked.Exchange(ref _gcPauseTimeSeconds, 0);
            if (_previousGcPauseTime.HasValue)
            {
                var extraPauseTimeMilliseconds = (gcPauseTimeSeconds * 1_000) - (_previousGcPauseTime.Value * 1000);
                statsd.Timer(MetricsNames.GcPauseTime, extraPauseTimeMilliseconds);
                Log.Debug($"Sent the following metrics to the DD agent: {MetricsNames.GcPauseTime}");
            }

            _previousGcPauseTime = gcPauseTimeSeconds;
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
                Interlocked.Increment(ref handler._totalConnections);
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

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        // Can't do these two:
        // _ = MetricsNames.ContentionTime;
        // _ = MetricsNames.GcMemoryLoad;

        // We want the following Meter/instruments
        // System.Runtime
        // - dotnet.gc.collections (tagged by gc.heap.generation=gen0): MetricsNames.Gen0CollectionsCount etc
        // - dotnet.gc.pause.time: MetricsNames.GcPauseTime
        // - dotnet.gc.last_collection.heap.size (gc.heap.generation=gen0/gen1/gen2/loh/poh): MetricsNames.Gen0HeapSize etc
        //
        // Microsoft.AspNetCore.Hosting
        // - http.server.active_requests: MetricsNames.AspNetCoreCurrentRequests
        // - http.server.request.duration: MetricsNames.AspNetCoreTotalRequests, MetricsNames.AspNetCoreFailedRequests
        // Microsoft.AspNetCore.Server.Kestrel
        // - kestrel.active_connections: MetricsNames.AspNetCoreCurrentConnections,
        // - kestrel.queued_connections: MetricsNames.AspNetCoreConnectionQueueLength,
        // - kestrel.connection.duration: MetricsNames.AspNetCoreTotalConnections,
        // - kestrel.queued_requests: MetricsNames.AspNetCoreRequestQueueLength
        var meterName = instrument.Meter.Name;
        var instrumentName = instrument.Name;
        if ((string.Equals(meterName, "Microsoft.AspNetCore.Hosting", StringComparison.Ordinal) && instrumentName is
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
                Log.Debug("Enabled measurement events for instrument: {MeterName}/{InstrumentName} ", instrumentName, meterName);
            }

            listener.EnableMeasurementEvents(instrument, state: this);
        }
    }
}

#endif
