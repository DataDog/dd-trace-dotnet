// <copyright file="MeterListenerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry.Metrics;
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
        [Fact]
        public async Task CreatesSeparateMetricPointsForDifferentTagSets()
        {
            var settings = TracerSettings.Create(new());
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

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
            await pipeline.ForceCollectAndExportAsync();
            var capturedMetrics = testExporter.ExportedMetrics;

            // Verify we have separate metric points for each tag set
            var counterMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();
            counterMetrics.Count.Should().Be(2, "Counter should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for counter metrics
            var counterWithTestValue = counterMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var counterWithNonDefaultValue = counterMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            counterWithTestValue.Should().NotBeNull("Counter should have a metric point with test_attr=test_value");
            counterWithNonDefaultValue.Should().NotBeNull("Counter should have a metric point with test_attr=non_default_value");

            // Verify SnapshotSum values for counter metrics
            counterWithTestValue!.SnapshotSum.Should().Be(10.0, "Counter with test_value should have SnapshotSum of 10");
            counterWithNonDefaultValue!.SnapshotSum.Should().Be(5.0, "Counter with non_default_value should have SnapshotSum of 5");

            var histogramMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.histogram").ToList();
            histogramMetrics.Count.Should().Be(2, "Histogram should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for histogram metrics
            var histogramWithTestValue = histogramMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var histogramWithNonDefaultValue = histogramMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            histogramWithTestValue.Should().NotBeNull("Histogram should have a metric point with test_attr=test_value");
            histogramWithNonDefaultValue.Should().NotBeNull("Histogram should have a metric point with test_attr=non_default_value");

            // Verify RunningSum values for histogram metrics
            histogramWithTestValue!.SnapshotSum.Should().Be(3.5, "Histogram with test_value should have SnapshotSum of 3.5");
            histogramWithNonDefaultValue!.SnapshotSum.Should().Be(2.1, "Histogram with non_default_value should have SnapshotSum of 2.1");

#if NET7_0_OR_GREATER
            var upDownMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.upDownCounter").ToList();
            upDownMetrics.Count.Should().Be(2, "UpDownCounter should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for upDownCounter metrics
            var upDownWithTestValue = upDownMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var upDownWithNonDefaultValue = upDownMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            upDownWithTestValue.Should().NotBeNull("UpDownCounter should have a metric point with test_attr=test_value");
            upDownWithNonDefaultValue.Should().NotBeNull("UpDownCounter should have a metric point with test_attr=non_default_value");

            // Verify SnapshotSum values for upDownCounter metrics
            upDownWithTestValue!.SnapshotSum.Should().Be(15.0, "UpDownCounter with test_value should have SnapshotSum of 15");
            upDownWithNonDefaultValue!.SnapshotSum.Should().Be(-8.0, "UpDownCounter with non_default_value should have SnapshotSum of -8");
#endif

#if NET9_0_OR_GREATER
            var gaugeMetrics = capturedMetrics.Where(m => m.InstrumentName == "test.gauge").ToList();
            gaugeMetrics.Count.Should().Be(2, "Gauge should have 2 separate metric points for different tag sets");

            // Verify the actual tag values for gauge metrics
            var gaugeWithTestValue = gaugeMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "test_value");
            var gaugeWithNonDefaultValue = gaugeMetrics.FirstOrDefault(m => m.Tags.ContainsKey("test_attr") && m.Tags["test_attr"]?.ToString() == "non_default_value");

            gaugeWithTestValue.Should().NotBeNull("Gauge should have a metric point with test_attr=test_value");
            gaugeWithNonDefaultValue.Should().NotBeNull("Gauge should have a metric point with test_attr=non_default_value");

            // Verify SnapshotGaugeValue values for gauge metrics
            gaugeWithTestValue!.SnapshotGaugeValue.Should().Be(42.0, "Gauge with test_value should have SnapshotGaugeValue of 42.0");
            gaugeWithNonDefaultValue!.SnapshotGaugeValue.Should().Be(99.0, "Gauge with non_default_value should have SnapshotGaugeValue of 99.0");
#endif
        }

        [Fact]
        public async Task DetectsDuplicateInstrumentsWithCaseInsensitiveNames()
        {
            // Arrange
            var settings = TracerSettings.Create(new());
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            var meter = new Meter("TestMeter");

            // Create instruments with same name but different casing within the same meter
            var counter1 = meter.CreateCounter<long>("duplicate.test.counter");
            var counter2 = meter.CreateCounter<long>("DUPLICATE.TEST.COUNTER"); // Different casing, same meter

            counter1.Add(1, new KeyValuePair<string, object>("tag1", "value1"));
            counter2.Add(2, new KeyValuePair<string, object>("tag2", "value2"));

            await pipeline.ForceCollectAndExportAsync();
            var capturedMetrics = testExporter.ExportedMetrics;

            capturedMetrics.Count.Should().Be(1, "Should have 1 metric point.");
            // Verify both metrics have different instrument names (case-sensitive)
            var instrumentNames = capturedMetrics.Select(m => m.InstrumentName).ToList();
            instrumentNames.Should().Contain("duplicate.test.counter");
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_Default()
        {
            _ = CapturesAllMetricsWithCorrectTemporality(
                temporalityPreference: null,
                expectedCounterTemporality: AggregationTemporality.Delta,
                expectedUpDownTemporality: AggregationTemporality.Cumulative,
                expectedHistogramTemporality: AggregationTemporality.Delta,
                expectedObservableCounterTemporality: AggregationTemporality.Delta,
                expectedObservableGaugeTemporality: null,
                expectedObservableUpDownTemporality: AggregationTemporality.Cumulative,
                expectedGaugeTemporality: null);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_DeltaPreference()
        {
            _ = CapturesAllMetricsWithCorrectTemporality(
                temporalityPreference: "delta",
                expectedCounterTemporality: AggregationTemporality.Delta,
                expectedUpDownTemporality: AggregationTemporality.Cumulative,
                expectedHistogramTemporality: AggregationTemporality.Delta,
                expectedObservableCounterTemporality: AggregationTemporality.Delta,
                expectedObservableGaugeTemporality: null,
                expectedObservableUpDownTemporality: AggregationTemporality.Cumulative,
                expectedGaugeTemporality: null);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_CumulativePreference()
        {
            _ = CapturesAllMetricsWithCorrectTemporality(
                temporalityPreference: "cumulative",
                expectedCounterTemporality: AggregationTemporality.Cumulative,
                expectedUpDownTemporality: AggregationTemporality.Cumulative,
                expectedHistogramTemporality: AggregationTemporality.Cumulative,
                expectedObservableCounterTemporality: AggregationTemporality.Cumulative,
                expectedObservableGaugeTemporality: null,
                expectedObservableUpDownTemporality: AggregationTemporality.Cumulative,
                expectedGaugeTemporality: null);
        }

        [Fact]
        public void CapturesAllMetricsWithCorrectTemporality_LowMemoryPreference()
        {
            _ = CapturesAllMetricsWithCorrectTemporality(
                temporalityPreference: "lowmemory",
                expectedCounterTemporality: AggregationTemporality.Delta,
                expectedUpDownTemporality: AggregationTemporality.Cumulative,
                expectedHistogramTemporality: AggregationTemporality.Delta,
                expectedObservableCounterTemporality: AggregationTemporality.Cumulative,
                expectedObservableGaugeTemporality: null,
                expectedObservableUpDownTemporality: AggregationTemporality.Cumulative,
                expectedGaugeTemporality: null);
        }

        private async Task CapturesAllMetricsWithCorrectTemporality(
            string temporalityPreference,
            AggregationTemporality expectedCounterTemporality,
            AggregationTemporality expectedUpDownTemporality,
            AggregationTemporality expectedHistogramTemporality,
            AggregationTemporality expectedObservableCounterTemporality,
            AggregationTemporality? expectedObservableGaugeTemporality,
            AggregationTemporality expectedObservableUpDownTemporality,
            AggregationTemporality? expectedGaugeTemporality)
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTemporalityPreference, temporalityPreference },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

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

            // Trigger collection and export
            await pipeline.ForceCollectAndExportAsync();
            var capturedMetrics = testExporter.ExportedMetrics;

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
            counterMetric.SnapshotSum.Should().Be(11.0);
            counterMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            counterMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");

            var asyncCounterMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.counter");
            asyncCounterMetric.Should().NotBeNull();
            asyncCounterMetric!.InstrumentType.Should().Be(InstrumentType.ObservableCounter);
            asyncCounterMetric.AggregationTemporality.Should().Be(expectedObservableCounterTemporality);
            asyncCounterMetric.SnapshotSum.Should().Be(22.0);
            asyncCounterMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var asyncGaugeMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.gauge");
            asyncGaugeMetric.Should().NotBeNull();
            asyncGaugeMetric!.InstrumentType.Should().Be(InstrumentType.ObservableGauge);
            asyncGaugeMetric.AggregationTemporality.Should().Be(expectedObservableGaugeTemporality, "ObservableGauge temporality should match expected value");
            asyncGaugeMetric.SnapshotGaugeValue.Should().Be(88.0);
            asyncGaugeMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");

            var histogramMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.histogram");
            histogramMetric.Should().NotBeNull();
            histogramMetric!.InstrumentType.Should().Be(InstrumentType.Histogram);
            histogramMetric.AggregationTemporality.Should().Be(expectedHistogramTemporality);
            histogramMetric.SnapshotCount.Should().Be(1L);
            histogramMetric.SnapshotSum.Should().Be(33.0);
            histogramMetric.SnapshotMin.Should().Be(33.0);
            histogramMetric.SnapshotMax.Should().Be(33.0);
            histogramMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
            histogramMetric.Tags.Should().ContainKey("rid").WhoseValue.Should().Be("1234567890");
            var bucketCounts = histogramMetric.SnapshotBucketCounts;
            bucketCounts[4].Should().Be(1L, "Value 33.0 should fall in bucket 4 (25 < 33.0 <= 50)");

#if NET7_0_OR_GREATER
            var upDownMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.upDownCounter");
            upDownMetric.Should().NotBeNull("UpDown counter metric should be captured");
            upDownMetric!.InstrumentType.Should().Be(InstrumentType.UpDownCounter);
            upDownMetric.AggregationTemporality.Should().Be(expectedUpDownTemporality);
            upDownMetric.SnapshotSum.Should().Be(55.0);
            upDownMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");

            var asyncUpDownMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.async.upDownCounter");
            asyncUpDownMetric.Should().NotBeNull("Async UpDown counter metric should be captured");
            asyncUpDownMetric!.InstrumentType.Should().Be(InstrumentType.ObservableUpDownCounter);
            asyncUpDownMetric.AggregationTemporality.Should().Be(expectedObservableUpDownTemporality);
            asyncUpDownMetric.SnapshotSum.Should().Be(66.0);
            asyncUpDownMetric.Tags.Count.Should().Be(0, "Async metrics have no tags");
#endif
#if NET9_0_OR_GREATER
            var gaugeMetric = capturedMetrics.FirstOrDefault(m => m.InstrumentName == "test.gauge");
            gaugeMetric.Should().NotBeNull("Gauge metric should be captured");
            gaugeMetric!.InstrumentType.Should().Be(InstrumentType.Gauge);
            gaugeMetric.AggregationTemporality.Should().Be(expectedGaugeTemporality, "Gauge temporality should match expected value");
            gaugeMetric.SnapshotGaugeValue.Should().Be(77.0);
            gaugeMetric.Tags.Should().ContainKey("http.method").WhoseValue.Should().Be("GET");
#endif
        }
    }
}
#endif
