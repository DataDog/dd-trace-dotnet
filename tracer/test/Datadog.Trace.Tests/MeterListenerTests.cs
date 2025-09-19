// <copyright file="MeterListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.OTelMetrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests
{
    [UsesVerify]
    [TracerRestorer]
    public class MeterListenerTests
    {
        [Theory]
        [InlineData("cumulative", AggregationTemporality.Cumulative, AggregationTemporality.Cumulative, AggregationTemporality.Cumulative, AggregationTemporality.Cumulative)]
        [InlineData("delta", AggregationTemporality.Delta, AggregationTemporality.Cumulative, AggregationTemporality.Delta, AggregationTemporality.Delta)]
        [InlineData("lowmemory", AggregationTemporality.Delta, AggregationTemporality.Cumulative, AggregationTemporality.Delta, AggregationTemporality.Cumulative)]
        public void CapturesAllMetricsWithCorrectTemporality(
            string temporalityPreference,
            AggregationTemporality expectedCounterTemporality,
            AggregationTemporality expectedUpDownTemporality,
            AggregationTemporality expectedHistogramTemporality,
            AggregationTemporality expectedObservableCounterTemporality)
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTemporalityPreference, temporalityPreference }
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            MetricReader.Initialize();

            // Create a test meter
            using var meter = new Meter("TestMeter");

            var testTags = new KeyValuePair<string, object>[]
            {
                new("http.method", "GET"),
                new("rid", "1234567890")
            };

            var counter = meter.CreateCounter<long>("test.counter");
            meter.CreateObservableCounter<long>("test.async.counter", () => 22L);
            meter.CreateObservableGauge<double>("test.async.gauge", () => 88L);
            var histogram = meter.CreateHistogram<double>("test.histogram");

            counter.Add(11L, testTags);
            histogram.Record(33L, testTags);

            var expectedMetricsCount = 4;

#if NET7_0_OR_GREATER
            var upDownCounter = meter.CreateUpDownCounter<long>("test.upDownCounter");
            meter.CreateObservableUpDownCounter<long>("test.async.upDownCounter", () => 66L);

            upDownCounter.Add(55L, testTags);
            expectedMetricsCount += 2;
#endif

#if NET9_0_OR_GREATER
            var gauge = meter.CreateGauge<double>("test.gauge");
            gauge.Record(77L, testTags);
            expectedMetricsCount += 1;
#endif

            // Trigger async collection and get metrics for testing
            MetricReader.CollectObservableInstruments();
            var capturedMetrics = MetricReaderHandler.GetCapturedMetricsForTesting();

            capturedMetrics.Count.Should().Be(expectedMetricsCount, $"Should capture exactly {expectedMetricsCount} metrics based on .NET version");

            // Verify expected temporality values are valid
            expectedCounterTemporality.Should().BeOneOf(AggregationTemporality.Delta, AggregationTemporality.Cumulative);
            expectedUpDownTemporality.Should().BeOneOf(AggregationTemporality.Delta, AggregationTemporality.Cumulative);
            expectedHistogramTemporality.Should().BeOneOf(AggregationTemporality.Delta, AggregationTemporality.Cumulative);
            expectedObservableCounterTemporality.Should().BeOneOf(AggregationTemporality.Delta, AggregationTemporality.Cumulative);

            // Verify Counter metrics
            var counterMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.counter");
            counterMetric.Should().NotBeNull();
            counterMetric.InstrumentType.Should().Be(InstrumentType.Counter);
            counterMetric.AggregationTemporality.Should().Be(expectedCounterTemporality);
            counterMetric.RunningSum.Should().Be(11.0);
            counterMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            counterMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");

            var asyncCounterMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.async.counter");
            asyncCounterMetric.Should().NotBeNull();
            asyncCounterMetric.InstrumentType.Should().Be(InstrumentType.ObservableCounter);
            asyncCounterMetric.AggregationTemporality.Should().Be(expectedObservableCounterTemporality);
            asyncCounterMetric.RunningSum.Should().Be(22.0);
            asyncCounterMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var asyncGaugeMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.async.gauge");
            asyncGaugeMetric.Should().NotBeNull();
            asyncGaugeMetric.InstrumentType.Should().Be(InstrumentType.ObservableGauge);
            asyncGaugeMetric.AggregationTemporality.Should().BeNull("Gauges have no temporality according to OTLP spec");
            asyncGaugeMetric.RunningSum.Should().Be(88.0);
            asyncGaugeMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var histogramMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.histogram");
            histogramMetric.Should().NotBeNull();
            histogramMetric.InstrumentType.Should().Be(InstrumentType.Histogram);
            histogramMetric.AggregationTemporality.Should().Be(expectedHistogramTemporality);
            histogramMetric.RunningCount.Should().Be(1L);
            histogramMetric.RunningSum.Should().Be(33.0);
            histogramMetric.RunningMin.Should().Be(33.0);
            histogramMetric.RunningMax.Should().Be(33.0);
            histogramMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            histogramMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");
            var bucketCounts = histogramMetric.RunningBucketCounts;
            bucketCounts[4].Should().Be(1L, "Value 33.0 should fall in bucket 4 (25 < 33.0 <= 50)");

#if NET7_0_OR_GREATER
            var upDownMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.upDownCounter");
            upDownMetric.Should().NotBeNull("UpDown counter metric should be captured");
            upDownMetric!.InstrumentType.Should().Be(InstrumentType.UpDownCounter);
            upDownMetric.AggregationTemporality.Should().Be(expectedUpDownTemporality);
            upDownMetric.RunningSum.Should().Be(55.0);
            upDownMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");

            var asyncUpDownMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.async.upDownCounter");
            asyncUpDownMetric.Should().NotBeNull("Async UpDown counter metric should be captured");
            asyncUpDownMetric!.InstrumentType.Should().Be(InstrumentType.ObservableUpDownCounter);
            asyncUpDownMetric.AggregationTemporality.Should().Be(expectedUpDownTemporality);
            asyncUpDownMetric.RunningSum.Should().Be(66.0);
            asyncUpDownMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");
#endif
#if NET9_0_OR_GREATER
            var gaugeMetric = capturedMetrics.Values.FirstOrDefault(m => m.InstrumentName == "test.gauge");
            gaugeMetric.Should().NotBeNull("Gauge metric should be captured");
            gaugeMetric!.InstrumentType.Should().Be(InstrumentType.Gauge);
            gaugeMetric.AggregationTemporality.Should().BeNull("Gauges have no temporality according to OTLP spec");
            gaugeMetric.RunningSum.Should().Be(77.0);
            gaugeMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
#endif
            MetricReader.Stop();
            MetricReaderHandler.ResetForTesting();
        }
    }
}
#endif
