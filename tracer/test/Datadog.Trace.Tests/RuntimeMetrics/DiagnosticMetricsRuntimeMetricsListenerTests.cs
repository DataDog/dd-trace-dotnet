// <copyright file="DiagnosticMetricsRuntimeMetricsListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;
using Range = Moq.Range;

namespace Datadog.Trace.Tests.RuntimeMetrics;

[CollectionDefinition(nameof(RuntimeEventListenerTests), DisableParallelization = true)]
[Collection(nameof(RuntimeEventListenerTests))]
public class DiagnosticMetricsRuntimeMetricsListenerTests
{
    [Fact]
    public void PushEvents()
    {
        var statsd = new Mock<IDogStatsd>();

        using var listener = new DiagnosticsMetricsRuntimeMetricsListener(new TestStatsdManager(statsd.Object));

        listener.Refresh();

        statsd.Verify(s => s.Gauge(MetricsNames.ThreadPoolWorkersCount, It.IsAny<double>(), 1, null), Times.Once);

        // some metrics are only recorded the _second_ time this is called, to avoid skewing the results at the start, so we just check for a couple
        statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.IsAny<double>(), 1, null), Times.Once);
    }

    [Fact]
    public void MonitorGarbageCollections()
    {
        var statsd = new Mock<IDogStatsd>();
        using var listener = new DiagnosticsMetricsRuntimeMetricsListener(new TestStatsdManager(statsd.Object));

        listener.Refresh();
        statsd.Invocations.Clear();

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        listener.Refresh();

        statsd.Verify(s => s.Gauge(MetricsNames.Gen0HeapSize, It.IsAny<double>(), It.IsAny<double>(), null), Times.AtLeastOnce);
        statsd.Verify(s => s.Gauge(MetricsNames.Gen1HeapSize,  It.IsAny<double>(), It.IsAny<double>(), null), Times.AtLeastOnce);
        statsd.Verify(s => s.Gauge(MetricsNames.Gen2HeapSize,  It.IsInRange(0d, long.MaxValue, Range.Exclusive), It.IsAny<double>(), null), Times.AtLeastOnce);
        statsd.Verify(s => s.Gauge(MetricsNames.LohSize, It.IsAny<double>(), It.IsAny<double>(), null), Times.AtLeastOnce);
#if NET9_0_OR_GREATER
        statsd.Verify(s => s.Timer(MetricsNames.GcPauseTime, It.IsAny<double>(), It.IsAny<double>(), null), Times.AtLeastOnce);
#endif
        statsd.Verify(s => s.Increment(MetricsNames.Gen2CollectionsCount, 1, It.IsAny<double>(), null), Times.AtLeastOnce);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void AspNetCore_Counters_ActiveRequests()
    {
        // based on https://github.com/dotnet/aspnetcore/blob/v10.0.1/src/Hosting/Hosting/src/Internal/HostingMetrics.cs
        using var meter = new Meter("Microsoft.AspNetCore.Hosting");

        // Pretending we're aspnetcore
        var instrument = meter.CreateUpDownCounter<long>(
            "http.server.active_requests",
            unit: "{request}",
            description: "Number of active HTTP server requests.");

        var statsd = new Mock<IDogStatsd>();
        using var listener = new DiagnosticsMetricsRuntimeMetricsListener(new TestStatsdManager(statsd.Object));

        listener.Refresh();
        statsd.Invocations.Clear();

        // First interval
        instrument.Add(1);
        instrument.Add(1);
        instrument.Add(1);
        listener.Refresh();
        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreCurrentRequests, 3.0, 1, null), Times.Once);
        statsd.Invocations.Clear();

        // Second interval
        instrument.Add(-1);
        listener.Refresh();
        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreCurrentRequests, 2.0, 1, null), Times.Once);
    }

    [Fact]
    public void AspNetCore_Counters_RequestCounts()
    {
        // based on https://github.com/dotnet/aspnetcore/blob/v10.0.1/src/Hosting/Hosting/src/Internal/HostingMetrics.cs
        using var meter = new Meter("Microsoft.AspNetCore.Hosting");

        // Pretending we're aspnetcore
        var instrument = meter.CreateHistogram<double>(
            "http.server.request.duration",
            unit: "s",
            description: "Duration of HTTP server requests.");

        var statsd = new Mock<IDogStatsd>();
        using var listener = new DiagnosticsMetricsRuntimeMetricsListener(new TestStatsdManager(statsd.Object));

        listener.Refresh();
        statsd.Invocations.Clear();

        // success requests
        instrument.Record(
            123,
            new TagList
            {
                { "url.scheme", "http" },
                { "http.request.method", "GET" },
                { "network.protocol.version", "1.1" },
                { "http.response.status_code", (object)200 },
                { "http.route", "/" }
            });

        instrument.Record(
            456,
            new TagList
            {
                { "url.scheme", "http" },
                { "http.request.method", "GET" },
                { "network.protocol.version", "1.1" },
                { "http.response.status_code", (object)201 },
                { "http.route", "/" }
            });

        // failed
        instrument.Record(
            789,
            new TagList
            {
                { "url.scheme", "https" },
                { "http.request.method", "POST" },
                { "network.protocol.version", "2.0" },
                { "http.response.status_code", (object)500 },
                { "http.route", "/" },
                { "error.type", typeof(ArgumentException).FullName }
            });

        listener.Refresh();

        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreFailedRequests, 1.0, 1, null), Times.Once);
        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreTotalRequests, 3.0, 1, null), Times.Once);
        statsd.Invocations.Clear();

        // success requests
        instrument.Record(
            1,
            new TagList
            {
                { "url.scheme", "http" },
                { "http.request.method", "GET" },
                { "network.protocol.version", "1.1" },
                { "http.response.status_code", (object)200 },
                { "http.route", "/" }
            });

        listener.Refresh();

        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreFailedRequests, 0.0, 1, null), Times.Once);
        statsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreTotalRequests, 1.0, 1, null), Times.Once);
    }

    [Fact]
    public void UpdateStatsdOnReinitialization()
    {
        // based on https://github.com/dotnet/aspnetcore/blob/v10.0.1/src/Hosting/Hosting/src/Internal/HostingMetrics.cs
        using var meter = new Meter("Microsoft.AspNetCore.Hosting");

        // Pretending we're aspnetcore
        var instrument = meter.CreateUpDownCounter<long>(
            "http.server.active_requests",
            unit: "{request}",
            description: "Number of active HTTP server requests.");

        var originalStatsd = new Mock<IDogStatsd>();
        var newStatsd = new Mock<IDogStatsd>();

        var settings = TracerSettings.Create(new() { { ConfigurationKeys.ServiceName, "original" } });
        var statsdManager = new StatsdManager(
            settings,
            (m, e) => new(m.ServiceName == "original" ? originalStatsd.Object : newStatsd.Object));
        statsdManager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        using var listener = new DiagnosticsMetricsRuntimeMetricsListener(statsdManager);

        listener.Refresh();
        originalStatsd.Invocations.Clear();

        // First interval
        instrument.Add(1);
        instrument.Add(1);
        instrument.Add(1);
        listener.Refresh();
        originalStatsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreCurrentRequests, 3.0, 1, null), Times.Once);
        originalStatsd.Invocations.Clear();

        // Updating the service name should trigger a new statsd client to be created
        settings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object> { { TracerSettingKeyConstants.ServiceNameKey, "updated" } }),
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        // Second interval
        instrument.Add(-1);
        listener.Refresh();
        newStatsd.Verify(s => s.Gauge(MetricsNames.AspNetCoreCurrentRequests, 2.0, 1, null), Times.Once);
    }
#endif
}
#endif
