// <copyright file="RuntimeMetricsMeterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry.Metrics;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics;

[UsesVerify]
[Collection(nameof(RuntimeEventListenerTests))]
[TracerRestorer]
public class RuntimeMetricsMeterTests
{
    [Fact]
    public async Task InstrumentSurface()
    {
        // On .NET 9+ the built-in System.Runtime meter publishes instruments natively.
        // On .NET 6-8 we rely on our polyfill.
        using var polyfill = Environment.Version.Major < 9 ? new RuntimeMetricsPolyfill() : null;

        var instruments = CapturePublishedInstruments(RuntimeMetricsPolyfill.MeterName);

        var snapshot = string.Join(
            "\n",
            instruments
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .Select(i => string.IsNullOrEmpty(i.Unit)
                    ? $"{i.Name} ({i.Type})"
                    : $"{i.Name} ({i.Type}, {i.Unit})"));

#if NET9_0_OR_GREATER
        const string tfm = "net9";
#else
        const string tfm = "net6";
#endif

        var settings = new VerifySettings();
        VerifyHelper.InitializeGlobalSettings();
        settings.UseFileName($"RuntimeMetricsMeterTests.InstrumentSurface_{tfm}");
        settings.DisableRequireUniquePrefix();

        await Verifier.Verify(snapshot, settings);
    }

    [Fact]
    public async Task SystemRuntimeMetricsFlowThroughOtlpPipeline()
    {
        var settings = TracerSettings.Create(new());
        var tracer = TracerHelper.CreateWithFakeAgent(settings);
        Tracer.UnsafeSetTracerInstance(tracer);

        var testExporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
        pipeline.Start();

        using var polyfill = Environment.Version.Major < 9 ? new RuntimeMetricsPolyfill() : null;

        GC.Collect(0, GCCollectionMode.Forced, blocking: true);

        await pipeline.ForceCollectAndExportAsync();

        var capturedMetrics = testExporter.ExportedMetrics;
        capturedMetrics.Should().NotBeEmpty("System.Runtime metrics should flow through the OTLP pipeline");

        var metricNames = capturedMetrics.Select(m => m.InstrumentName).Distinct().ToList();
        metricNames.Should().Contain("dotnet.gc.collections");
        metricNames.Should().Contain("dotnet.thread_pool.thread.count");
        metricNames.Should().Contain("dotnet.process.memory.working_set");

        foreach (var metric in capturedMetrics.Where(m => m.MeterName == RuntimeMetricsPolyfill.MeterName))
        {
            metric.MeterName.Should().Be(RuntimeMetricsPolyfill.MeterName);
        }
    }

    private static List<InstrumentInfo> CapturePublishedInstruments(string meterName)
    {
        var instruments = new List<InstrumentInfo>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
            {
                var typeName = instrument.GetType().Name;
                var cleanType = typeName.Contains('`') ? typeName[..typeName.IndexOf('`')] : typeName;
                instruments.Add(new InstrumentInfo(instrument.Name, cleanType, instrument.Unit ?? string.Empty));
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();

        return instruments;
    }

    private record InstrumentInfo(string Name, string Type, string Unit);
}

#endif
