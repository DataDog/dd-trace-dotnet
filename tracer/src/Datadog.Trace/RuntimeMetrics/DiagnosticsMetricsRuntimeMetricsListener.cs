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
    private static readonly string[] GcGenMetricNames = [MetricsNames.Gen0HeapSize, MetricsNames.Gen1HeapSize, MetricsNames.Gen2HeapSize, MetricsNames.LohSize];

    private static readonly int MaxGcGenerations = Math.Min(GC.GetGCMemoryInfo().GenerationInfo.Length, GcGenMetricNames.Length);

    private readonly IStatsdManager _statsd;
    private readonly MeterListener _listener;
    private readonly bool _systemRuntimeMetricsAvailable;
    private readonly bool _aspnetcoreMetricsAvailable;

    private long _gen0Size;
    private long _gen1Size;
    private long _gen2Size;
    private long _lohSize;
    private long _gen0Count;
    private long _gen1Count;
    private long _gen2Count;
    private double _gcPauseTimeSeconds;

    private long _activeRequests;
    private long _failedRequests;
    private long _successRequests;
    private long _queuedRequests;
    private long _activeConnections;
    private long _queuedConnections;
    private long _totalConnections;

    private long? _previousGen0Count;
    private long? _previousGen1Count;
    private long? _previousGen2Count;
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

        // These we can grab directly using existing APIs, without needing the metrics API
        statsd.Gauge(MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);

        var contentionCount = Monitor.LockContentionCount;
        if (_previousContentionCount.HasValue)
        {
            statsd.Counter(MetricsNames.ContentionCount, contentionCount - _previousContentionCount.Value);
        }

        _previousContentionCount = contentionCount;

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
        }

        if (_systemRuntimeMetricsAvailable)
        {
            statsd.Gauge(MetricsNames.Gen0HeapSize, Interlocked.Exchange(ref _gen0Size, 0));
            statsd.Gauge(MetricsNames.Gen1HeapSize, Interlocked.Exchange(ref _gen1Size, 0));
            statsd.Gauge(MetricsNames.Gen2HeapSize, Interlocked.Exchange(ref _gen2Size, 0));
            statsd.Gauge(MetricsNames.LohSize, Interlocked.Exchange(ref _lohSize, 0));

            var gen0Count = Interlocked.Exchange(ref _gen0Count, 0);
            var gen1Count = Interlocked.Exchange(ref _gen1Count, 0);
            var gen2Count = Interlocked.Exchange(ref _gen2Count, 0);

            if (_previousGen0Count.HasValue)
            {
                statsd.Increment(MetricsNames.Gen0CollectionsCount, (int)Math.Min(gen0Count - _previousGen0Count.Value, int.MaxValue));
            }

            if (_previousGen1Count.HasValue)
            {
                statsd.Increment(MetricsNames.Gen1CollectionsCount, (int)Math.Min(gen1Count - _previousGen1Count.Value, int.MaxValue));
            }

            if (_previousGen2Count.HasValue)
            {
                statsd.Increment(MetricsNames.Gen2CollectionsCount, (int)Math.Min(gen2Count - _previousGen2Count.Value, int.MaxValue));
            }

            _previousGen0Count = gen0Count;
            _previousGen1Count = gen1Count;
            _previousGen2Count = gen2Count;

            var gcPauseTimeSeconds = Interlocked.Exchange(ref _gcPauseTimeSeconds, 0);
            if (_previousGcPauseTime.HasValue)
            {
                var extraPauseTimeMilliseconds = (gcPauseTimeSeconds * 1_000) - (_previousGcPauseTime.Value * 1000);
                statsd.Timer(MetricsNames.GcPauseTime, extraPauseTimeMilliseconds);
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

            case "dotnet.gc.collections":
                foreach (var tagPair in tags)
                {
                    if (tagPair is { Key: "gc.heap.generation", Value: string tag })
                    {
                        switch (tag)
                        {
                            case "gen0":
                                Interlocked.Exchange(ref handler._gen0Count, measurement);
                                break;
                            case "gen1":
                                Interlocked.Exchange(ref handler._gen1Count, measurement);
                                break;
                            case "gen2":
                                Interlocked.Exchange(ref handler._gen2Count, measurement);
                                break;
                        }

                        return;
                    }
                }

                break;

            case "dotnet.gc.last_collection.heap.size":
                foreach (var tagPair in tags)
                {
                    if (tagPair is { Key: "gc.heap.generation", Value: string tag })
                    {
                        switch (tag)
                        {
                            case "gen0":
                                Interlocked.Exchange(ref handler._gen0Size, measurement);
                                break;
                            case "gen1":
                                Interlocked.Exchange(ref handler._gen1Size, measurement);
                                break;
                            case "gen2":
                                Interlocked.Exchange(ref handler._gen2Size, measurement);
                                break;
                            case "loh":
                                Interlocked.Exchange(ref handler._lohSize, measurement);
                                break;
                        }

                        return;
                    }
                }

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
        if ((string.Equals(meterName, "System.Runtime", StringComparison.Ordinal) && instrumentName is
                 "dotnet.gc.collections" or
                 "dotnet.gc.pause.time" or
                 "dotnet.gc.last_collection.heap.size")
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
                Log.Debug("Enabled measurement events for instrument: {MeterName}/{InstrumentName} ", instrumentName, meterName);
            }

            listener.EnableMeasurementEvents(instrument, state: this);
        }
    }
}

#endif
