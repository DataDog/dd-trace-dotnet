// <copyright file="MeterListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
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
    public class MeterListenerTests : IDisposable
    {
        public void Dispose()
        {
            MetricReader.Stop();
            MetricReaderHandler.ResetForTesting();
        }

        [Fact]
        public void CreatesSeparateMetricPointsForDifferentTagSets()
        {
            var settings = TracerSettings.Create(new());
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            MetricReader.Initialize();

            // Create a test meter
            using var meter = new Meter("TestMeter");

            var defaultTags = new KeyValuePair<string, object>[]
            {
                new("test_attr", "test_value")
            };

            var nonDefaultTags = new KeyValuePair<string, object>[]
            {
                new("test_attr", "non_default_value")
            };

            // Test Counter
            var counter = meter.CreateCounter<long>("test.counter");
            counter.Add(10, defaultTags);
            counter.Add(5, nonDefaultTags);

            // Test Histogram
            var histogram = meter.CreateHistogram<double>("test.histogram");
            histogram.Record(3.5, defaultTags);
            histogram.Record(2.1, nonDefaultTags);

#if NET7_0_OR_GREATER
            // Test UpDownCounter
            var upDownCounter = meter.CreateUpDownCounter<long>("test.upDownCounter");
            upDownCounter.Add(15, defaultTags);
            upDownCounter.Add(-8, nonDefaultTags);
#endif

#if NET9_0_OR_GREATER
            // Test Gauge
            var gauge = meter.CreateGauge<double>("test.gauge");
            gauge.Record(42.0, defaultTags);
            gauge.Record(99.0, nonDefaultTags);
#endif

            // Collect metrics
            MetricReader.CollectObservableInstruments();
            var capturedMetrics = MetricReaderHandler.GetCapturedMetricsForTesting();

            // Verify we have separate metric points for each tag set
            var counterMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();
            counterMetrics.Count.Should().Be(2, "Counter should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for counter metrics
            var counterWithTestValue = counterMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var counterWithNonDefaultValue = counterMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            counterWithTestValue.Should().NotBeNull("Counter should have a metric point with test_attr=test_value");
            counterWithNonDefaultValue.Should().NotBeNull("Counter should have a metric point with test_attr=non_default_value");

            var histogramMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.histogram").ToList();
            histogramMetrics.Count.Should().Be(2, "Histogram should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for histogram metrics
            var histogramWithTestValue = histogramMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var histogramWithNonDefaultValue = histogramMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            histogramWithTestValue.Should().NotBeNull("Histogram should have a metric point with test_attr=test_value");
            histogramWithNonDefaultValue.Should().NotBeNull("Histogram should have a metric point with test_attr=non_default_value");

#if NET7_0_OR_GREATER
            var upDownMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.upDownCounter").ToList();
            upDownMetrics.Count.Should().Be(2, "UpDownCounter should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for upDownCounter metrics
            var upDownWithTestValue = upDownMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var upDownWithNonDefaultValue = upDownMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            upDownWithTestValue.Should().NotBeNull("UpDownCounter should have a metric point with test_attr=test_value");
            upDownWithNonDefaultValue.Should().NotBeNull("UpDownCounter should have a metric point with test_attr=non_default_value");
#endif

#if NET9_0_OR_GREATER
            var gaugeMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.gauge").ToList();
            gaugeMetrics.Count.Should().Be(2, "Gauge should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for gauge metrics
            var gaugeWithTestValue = gaugeMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var gaugeWithNonDefaultValue = gaugeMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            gaugeWithTestValue.Should().NotBeNull("Gauge should have a metric point with test_attr=test_value");
            gaugeWithNonDefaultValue.Should().NotBeNull("Gauge should have a metric point with test_attr=non_default_value");
#endif
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_Default()
        {
            CapturesAllMetricsWithCorrectTemporality(
                null,
                AggregationTemporality.Delta,
                AggregationTemporality.Cumulative,
                AggregationTemporality.Delta,
                AggregationTemporality.Delta);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_DeltaPreference()
        {
            CapturesAllMetricsWithCorrectTemporality(
                "delta",
                AggregationTemporality.Delta,
                AggregationTemporality.Cumulative,
                AggregationTemporality.Delta,
                AggregationTemporality.Delta);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_CumulativePreference()
        {
            CapturesAllMetricsWithCorrectTemporality(
                "cumulative",
                AggregationTemporality.Cumulative,
                AggregationTemporality.Cumulative,
                AggregationTemporality.Cumulative,
                AggregationTemporality.Cumulative);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_LowMemoryPreference()
        {
            CapturesAllMetricsWithCorrectTemporality(
                "lowmemory",
                AggregationTemporality.Delta,
                AggregationTemporality.Cumulative,
                AggregationTemporality.Delta,
                AggregationTemporality.Cumulative);
        }

        private void CapturesAllMetricsWithCorrectTemporality(
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
            var counterMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.counter");
            counterMetric.Should().NotBeNull();
            counterMetric!.InstrumentType.Should().Be(InstrumentType.Counter);
            counterMetric.AggregationTemporality.Should().Be(expectedCounterTemporality);
            counterMetric.RunningSum.Should().Be(11.0);
            counterMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            counterMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");

            var asyncCounterMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.counter");
            asyncCounterMetric.Should().NotBeNull();
            asyncCounterMetric!.InstrumentType.Should().Be(InstrumentType.ObservableCounter);
            asyncCounterMetric.AggregationTemporality.Should().Be(expectedObservableCounterTemporality);
            asyncCounterMetric.RunningSum.Should().Be(22.0);
            asyncCounterMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var asyncGaugeMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.gauge");
            asyncGaugeMetric.Should().NotBeNull();
            asyncGaugeMetric!.InstrumentType.Should().Be(InstrumentType.ObservableGauge);
            asyncGaugeMetric.AggregationTemporality.Should().BeNull("Gauges have no temporality according to OTLP spec");
            asyncGaugeMetric.RunningSum.Should().Be(88.0);
            asyncGaugeMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var histogramMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.histogram");
            histogramMetric.Should().NotBeNull();
            histogramMetric!.InstrumentType.Should().Be(InstrumentType.Histogram);
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
            var upDownMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.upDownCounter");
            upDownMetric.Should().NotBeNull("UpDown counter metric should be captured");
            upDownMetric!.InstrumentType.Should().Be(InstrumentType.UpDownCounter);
            upDownMetric.AggregationTemporality.Should().Be(expectedUpDownTemporality);
            upDownMetric.RunningSum.Should().Be(55.0);
            upDownMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");

            var asyncUpDownMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.upDownCounter");
            asyncUpDownMetric.Should().NotBeNull("Async UpDown counter metric should be captured");
            asyncUpDownMetric!.InstrumentType.Should().Be(InstrumentType.ObservableUpDownCounter);
            asyncUpDownMetric.AggregationTemporality.Should().Be(expectedUpDownTemporality);
            asyncUpDownMetric.RunningSum.Should().Be(66.0);
            asyncUpDownMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");
#endif
#if NET9_0_OR_GREATER
            var gaugeMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.gauge");
            gaugeMetric.Should().NotBeNull("Gauge metric should be captured");
            gaugeMetric!.InstrumentType.Should().Be(InstrumentType.Gauge);
            gaugeMetric.AggregationTemporality.Should().BeNull("Gauges have no temporality according to OTLP spec");
            gaugeMetric.RunningSum.Should().Be(77.0);
            gaugeMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
#endif
        }
    }
}
#endif
