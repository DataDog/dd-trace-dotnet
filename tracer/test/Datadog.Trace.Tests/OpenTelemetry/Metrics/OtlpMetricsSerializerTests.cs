// <copyright file="OtlpMetricsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry.Metrics;
using FluentAssertions;
using Xunit;
using MetricsData = OpenTelemetry.Proto.Metrics.V1.MetricsData;
using NumberDataPoint = OpenTelemetry.Proto.Metrics.V1.NumberDataPoint;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Datadog.Trace.Tests.OpenTelemetry.Metrics;

// Shares the MeterListenerTests collection: MeterListener is process-global, so a concurrently
// created Meter would be captured by this pipeline.
[Collection(nameof(MeterListenerTests))]
public class OtlpMetricsSerializerTests
{
#if NET10_0_OR_GREATER
    [Fact]
    public async Task MetersThatDifferOnlyBySchemaUrlAreExportedAsSeparateScopes()
    {
        var settings = TracerSettings.Create(new());
        var exporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, exporter);
        pipeline.Start();

        using var meter1 = new Meter(new MeterOptions("TestMeter.SchemaUrl") { TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.0.0" });
        using var meter2 = new Meter(new MeterOptions("TestMeter.SchemaUrl") { TelemetrySchemaUrl = "https://opentelemetry.io/schemas/2.0.0" });
        meter1.CreateCounter<long>("test.schema.counter").Add(1);
        meter2.CreateCounter<long>("test.schema.counter").Add(2);
        await pipeline.ForceCollectAndExportAsync();

        var scopes = MetricsData.Parser.ParseFrom(new OtlpMetricsSerializer(settings).SerializeMetrics(exporter.ExportedMetrics.ToList()))
                                .ResourceMetrics.SelectMany(r => r.ScopeMetrics)
                                .Where(s => s.Scope.Name == "TestMeter.SchemaUrl");

        scopes.Select(s => (s.SchemaUrl, s.Metrics.Single().Sum.DataPoints.Single().AsInt))
              .Should().BeEquivalentTo([("https://opentelemetry.io/schemas/1.0.0", 1L), ("https://opentelemetry.io/schemas/2.0.0", 2L)]);
    }
#endif

#if NET8_0_OR_GREATER
    [Fact]
    public async Task MeterTagValuesAreFormattedTheSameWayWhateverTheThreadCulture()
    {
        var settings = TracerSettings.Create(new());
        var exporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, exporter);
        pipeline.Start();

        const string name = "test.culture.counter";
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // An instrument is registered on the thread that creates it: here fr-FR, where the double 1.5 prints "1,5",
            // the same text as the string "1,5".
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            using var doubleMeter = new Meter("TestMeter.Culture", null, [new KeyValuePair<string, object>("ratio", 1.5)]);
            using var stringMeter = new Meter("TestMeter.Culture", null, [new KeyValuePair<string, object>("ratio", "1,5")]);
            doubleMeter.CreateCounter<long>(name).Add(5);
            stringMeter.CreateCounter<long>(name).Add(3);

            // Exported under en-US, where 1.5 prints "1.5": each meter keeps its own scope and attribute text.
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            await pipeline.ForceCollectAndExportAsync();

            var scopes = MetricsData.Parser.ParseFrom(new OtlpMetricsSerializer(settings).SerializeMetrics(exporter.ExportedMetrics.ToList()))
                                    .ResourceMetrics.SelectMany(r => r.ScopeMetrics)
                                    .Where(s => s.Scope.Name == "TestMeter.Culture");

            scopes.Select(s => (s.Scope.Attributes.Single(a => a.Key == "ratio").Value.StringValue, s.Metrics.Single().Sum.DataPoints.Single().AsInt))
                  .Should().BeEquivalentTo([("1.5", 5L), ("1,5", 3L)]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
#endif

    [Fact]
    public async Task DataPointTagValuesAreFormattedWithTheInvariantCulture()
    {
        var settings = TracerSettings.Create(new());
        var exporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, exporter);
        pipeline.Start();

        const string name = "test.culture.point.counter";
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Under fr-FR the double 1.5 prints "1,5", the same text as the string "1,5": both points must keep their
            // own attribute text instead of being exported as two points with identical attributes.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            using var meter = new Meter("TestMeter.PointCulture");
            var counter = meter.CreateCounter<long>(name);
            counter.Add(5, new KeyValuePair<string, object>("ratio", 1.5));
            counter.Add(3, new KeyValuePair<string, object>("ratio", "1,5"));

            await pipeline.ForceCollectAndExportAsync();

            var points = MetricsData.Parser.ParseFrom(new OtlpMetricsSerializer(settings).SerializeMetrics(exporter.ExportedMetrics.ToList()))
                                    .ResourceMetrics.SelectMany(r => r.ScopeMetrics).SelectMany(s => s.Metrics)
                                    .Single(m => m.Name == name).Sum.DataPoints;

            points.Select(p => (p.Attributes.Single(a => a.Key == "ratio").Value.StringValue, p.AsInt))
                  .Should().BeEquivalentTo([("1.5", 5L), ("1,5", 3L)]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task IntegerAndFloatingPointInstrumentsWithTheSameNameAreExportedAsSeparateMetrics()
    {
        var settings = TracerSettings.Create(new());
        var exporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, exporter);
        pipeline.Start();

        using var meter = new Meter("TestMeter.NumberType");
        meter.CreateCounter<long>("test.numbertype.counter").Add(10);
        meter.CreateCounter<double>("test.numbertype.counter").Add(2.5);
        await pipeline.ForceCollectAndExportAsync();

        var metrics = MetricsData.Parser.ParseFrom(new OtlpMetricsSerializer(settings).SerializeMetrics(exporter.ExportedMetrics.ToList()))
                                 .ResourceMetrics.SelectMany(r => r.ScopeMetrics)
                                 .Where(s => s.Scope.Name == "TestMeter.NumberType")
                                 .SelectMany(s => s.Metrics)
                                 .ToList();

        metrics.Should().HaveCount(2);
        metrics.Should().OnlyContain(m => m.Name == "test.numbertype.counter" && m.Sum.DataPoints.Count == 1);
        metrics.Select(m => m.Sum.DataPoints.Single())
               .Select(p => p.ValueCase == NumberDataPoint.ValueOneofCase.AsInt ? (object)p.AsInt : p.AsDouble)
               .Should().BeEquivalentTo<object>([10L, 2.5]);
    }

    [Theory]
    [InlineData("cumulative", "Cumulative", "Cumulative")]
    [InlineData("delta", "Delta", "Delta")]
    [InlineData("lowmemory", "Delta", "Cumulative")]
    [InlineData("LOWMEMORY", "Delta", "Cumulative")]
    [InlineData("LoWmEmOrY", "Delta", "Cumulative")]
    public async Task SerializedTemporalityMatchesAggregationTemporality(string preference, string syncTemporality, string observableCounterTemporality)
    {
        var expectedSync = Enum.Parse<AggregationTemporality>(syncTemporality);
        var expectedObservableCounter = Enum.Parse<AggregationTemporality>(observableCounterTemporality);

        var settings = TracerSettings.Create(new() { { ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTemporalityPreference, preference } });
        var exporter = new InMemoryExporter();
        await using var pipeline = new OtelMetricsPipeline(settings, exporter);
        pipeline.Start();

        using var meter = new Meter("TestMeter");
        long observed = 0;
        var counter = meter.CreateCounter<long>("test.counter");
        meter.CreateObservableCounter("test.async.counter", () => observed);
        var histogram = meter.CreateHistogram<double>("test.histogram");
#if NET7_0_OR_GREATER
        var upDownCounter = meter.CreateUpDownCounter<long>("test.upDownCounter");
        meter.CreateObservableUpDownCounter("test.async.upDownCounter", () => observed);
#endif

        // Two collection cycles, so that delta and cumulative values differ
        foreach (var observation in new long[] { 10, 30 })
        {
            observed = observation;
            counter.Add(5);
            histogram.Record(4);
#if NET7_0_OR_GREATER
            upDownCounter.Add(5);
#endif
            await pipeline.ForceCollectAndExportAsync();
        }

        var lastCycle = exporter.ExportedMetrics.GroupBy(m => m.InstrumentName).Select(g => g.Last()).ToList();
        var exported = MetricsData.Parser.ParseFrom(new OtlpMetricsSerializer(settings).SerializeMetrics(lastCycle))
                                  .ResourceMetrics.SelectMany(r => r.ScopeMetrics).SelectMany(s => s.Metrics)
                                  .ToDictionary(m => m.Name, ReadTemporalityAndValue);

        foreach (var point in lastCycle)
        {
            exported[point.InstrumentName].Temporality.Should().Be(point.AggregationTemporality, point.InstrumentName);
        }

        AssertExported("test.counter", expectedSync, deltaValue: 5, cumulativeValue: 10);
        AssertExported("test.histogram", expectedSync, deltaValue: 4, cumulativeValue: 8);
        AssertExported("test.async.counter", expectedObservableCounter, deltaValue: 20, cumulativeValue: 30);
#if NET7_0_OR_GREATER
        AssertExported("test.upDownCounter", AggregationTemporality.Cumulative, deltaValue: 5, cumulativeValue: 10);
        AssertExported("test.async.upDownCounter", AggregationTemporality.Cumulative, deltaValue: 20, cumulativeValue: 30);
#endif

        void AssertExported(string name, AggregationTemporality temporality, double deltaValue, double cumulativeValue)
        {
            exported[name].Temporality.Should().Be(temporality, name);
            exported[name].Value.Should().Be(temporality == AggregationTemporality.Delta ? deltaValue : cumulativeValue, name);
        }
    }

    private static (AggregationTemporality? Temporality, double Value) ReadTemporalityAndValue(OtlpMetric metric)
    {
        switch (metric.DataCase)
        {
            case OtlpMetric.DataOneofCase.Sum:
                var point = metric.Sum.DataPoints.Single();
                var value = point.ValueCase == NumberDataPoint.ValueOneofCase.AsInt ? point.AsInt : point.AsDouble;
                return ((AggregationTemporality)metric.Sum.AggregationTemporality, value);
            case OtlpMetric.DataOneofCase.Histogram:
                return ((AggregationTemporality)metric.Histogram.AggregationTemporality, metric.Histogram.DataPoints.Single().Sum);
            default:
                return (null, double.NaN);
        }
    }
}

#endif
