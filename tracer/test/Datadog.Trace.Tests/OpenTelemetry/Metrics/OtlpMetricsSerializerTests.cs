// <copyright file="OtlpMetricsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Diagnostics.Metrics;
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
