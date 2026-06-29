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
        public async Task CapsCardinalityAndAggregatesExcessIntoOverflowPoint()
        {
            // Limit the stream to 3 distinct tag sets so the test stays small and deterministic.
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "3" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<long>("test.counter");

            // First 3 distinct tag sets fill the cardinality budget.
            counter.Add(1, new KeyValuePair<string, object>("k", "v0"));
            counter.Add(1, new KeyValuePair<string, object>("k", "v1"));
            counter.Add(1, new KeyValuePair<string, object>("k", "v2"));

            // Subsequent *new* tag sets are routed to the single overflow point.
            counter.Add(10, new KeyValuePair<string, object>("k", "v3"));
            counter.Add(20, new KeyValuePair<string, object>("k", "v4"));

            // An already-tracked tag set keeps aggregating, even past the limit.
            counter.Add(5, new KeyValuePair<string, object>("k", "v0"));

            await pipeline.ForceCollectAndExportAsync();
            var counterMetrics = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();

            // 3 regular points + 1 overflow point.
            counterMetrics.Count.Should().Be(4, "should keep 3 regular points plus 1 overflow point");

            var overflowPoints = counterMetrics.Where(m => m.Tags.ContainsKey("otel.metric.overflow")).ToList();
            overflowPoints.Count.Should().Be(1, "there should be exactly one overflow point");
            overflowPoints[0].Tags["otel.metric.overflow"].Should().Be("true");
            overflowPoints[0].Tags.Count.Should().Be(1, "the overflow point should carry only the overflow attribute");
            overflowPoints[0].SnapshotSum.Should().Be(30.0, "the overflow point should aggregate the measurements that exceeded the limit (10 + 20)");

            var regularPoints = counterMetrics.Where(m => !m.Tags.ContainsKey("otel.metric.overflow")).ToList();
            regularPoints.Count.Should().Be(3);
            regularPoints
                .Where(m => m.Tags.TryGetValue("k", out var v) && (v?.ToString() == "v3" || v?.ToString() == "v4"))
                .Should().BeEmpty("excess tag sets must not get their own points");

            var v0 = regularPoints.FirstOrDefault(m => m.Tags.TryGetValue("k", out var v) && v?.ToString() == "v0");
            v0.Should().NotBeNull();
            v0!.SnapshotSum.Should().Be(6.0, "the existing v0 point should aggregate both of its measurements (1 + 5)");
        }

        [Fact]
        public async Task DoesNotCreateOverflowPointBelowLimit()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "3" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<long>("test.counter");

            counter.Add(1, new KeyValuePair<string, object>("k", "v0"));
            counter.Add(1, new KeyValuePair<string, object>("k", "v1"));

            await pipeline.ForceCollectAndExportAsync();
            var counterMetrics = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();

            counterMetrics.Count.Should().Be(2);
            counterMetrics.Should().NotContain(m => m.Tags.ContainsKey("otel.metric.overflow"), "no overflow point should be created below the cardinality limit");
        }

        [Fact]
        public async Task CardinalityCapIsAppliedOverStreamLifetimeAcrossExportCycles()
        {
            // Counters default to Delta temporality: running values reset after each export, but the
            // set of tracked points (and the cardinality count) persist for the lifetime of the stream.
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "3" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<long>("test.counter");

            // Cycle 1: fill the budget (v0, v1, v2) then overflow (v3, v4).
            counter.Add(1, new KeyValuePair<string, object>("k", "v0"));
            counter.Add(1, new KeyValuePair<string, object>("k", "v1"));
            counter.Add(1, new KeyValuePair<string, object>("k", "v2"));
            counter.Add(10, new KeyValuePair<string, object>("k", "v3"));
            counter.Add(20, new KeyValuePair<string, object>("k", "v4"));

            await pipeline.ForceCollectAndExportAsync();
            var cycle1 = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();

            cycle1.Count.Should().Be(4, "cycle 1 should export 3 regular points + 1 overflow point");
            cycle1.Single(m => m.Tags.ContainsKey("otel.metric.overflow")).SnapshotSum.Should().Be(30.0, "overflow should aggregate v3 + v4 (10 + 20)");

            var exportedAfterCycle1 = testExporter.ExportedMetrics.Count;

            // Cycle 2: re-touch an existing regular point (v0) and introduce a brand-new tag set (v5).
            // Because the cap is over the stream lifetime, the budget is still full, so v5 must overflow
            // and must NOT get its own point.
            counter.Add(7, new KeyValuePair<string, object>("k", "v0"));
            counter.Add(3, new KeyValuePair<string, object>("k", "v5"));

            await pipeline.ForceCollectAndExportAsync();
            var cycle2 = testExporter.ExportedMetrics.Skip(exportedAfterCycle1).Where(m => m.InstrumentName == "test.counter").ToList();

            // Only points with new measurements are exported: v0 and the overflow point. v1/v2 had no
            // new data this cycle, so they are omitted.
            cycle2.Count.Should().Be(2, "cycle 2 should export only the points that received new measurements (v0 + overflow)");

            cycle2.Where(m => m.Tags.TryGetValue("k", out var v) && v?.ToString() == "v5")
                  .Should().BeEmpty("a new tag set must still overflow once the lifetime budget is full");

            var v0 = cycle2.Single(m => m.Tags.TryGetValue("k", out var v) && v?.ToString() == "v0");
            v0.SnapshotSum.Should().Be(7.0, "delta temporality resets running values, so v0 reflects only its cycle-2 measurement");

            cycle2.Single(m => m.Tags.ContainsKey("otel.metric.overflow")).SnapshotSum.Should().Be(3.0, "the overflow point's value also resets between delta cycles, so it reflects only v5");
        }

        [Fact]
        public async Task HistogramMeasurementsBeyondLimitAggregateIntoOverflowPoint()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "2" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var histogram = meter.CreateHistogram<double>("test.histogram");

            // First 2 distinct tag sets fill the budget.
            histogram.Record(1.0, new KeyValuePair<string, object>("k", "v0"));
            histogram.Record(1.0, new KeyValuePair<string, object>("k", "v1"));

            // Excess tag sets overflow into a single histogram point that aggregates count/sum/buckets.
            histogram.Record(3.0, new KeyValuePair<string, object>("k", "v2"));
            histogram.Record(7.0, new KeyValuePair<string, object>("k", "v3"));

            await pipeline.ForceCollectAndExportAsync();
            var histogramMetrics = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.histogram").ToList();

            histogramMetrics.Count.Should().Be(3, "should keep 2 regular histogram points plus 1 overflow point");

            histogramMetrics
                .Where(m => m.Tags.TryGetValue("k", out var v) && (v?.ToString() == "v2" || v?.ToString() == "v3"))
                .Should().BeEmpty("excess histogram tag sets must not get their own points");

            var overflow = histogramMetrics.Single(m => m.Tags.ContainsKey("otel.metric.overflow"));
            overflow.Tags["otel.metric.overflow"].Should().Be("true");
            overflow.SnapshotCount.Should().Be(2, "the overflow histogram should count both excess measurements (3.0 and 7.0)");
            overflow.SnapshotSum.Should().Be(10.0, "the overflow histogram should sum both excess measurements (3.0 + 7.0)");
        }

        [Fact]
        public async Task ConcurrentRecordingConservesAllMeasurementsAndBoundsCardinality()
        {
            const int threadCount = 8;
            const int perThread = 100;
            const int limit = 50;

            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, limit.ToString() },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<long>("test.counter");

            // Each thread records perThread distinct tag sets (unique across threads), one increment each.
            var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
            {
                for (var i = 0; i < perThread; i++)
                {
                    counter.Add(1, new KeyValuePair<string, object>("k", $"t{t}_v{i}"));
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            await pipeline.ForceCollectAndExportAsync();

            var counterMetrics = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.counter").ToList();

            // Conservation: every increment must land somewhere (a regular point or the overflow point),
            // regardless of races. No measurement may be lost.
            counterMetrics.Sum(m => m.SnapshotSum).Should().Be(threadCount * perThread, "every recorded increment must be aggregated into some point");

            counterMetrics.Count(m => m.Tags.ContainsKey("otel.metric.overflow")).Should().Be(1, "the excess measurements collapse into a single overflow point");

            var regularPointCount = counterMetrics.Count(m => !m.Tags.ContainsKey("otel.metric.overflow"));

            // The lock-free counter may let the dictionary exceed the limit by a bounded amount under
            // concurrency (roughly one per racing thread), but never unbounded.
            regularPointCount.Should().BeInRange(limit, limit + threadCount, "regular points should be capped near the limit, with only bounded overshoot from races");
        }

        [Fact]
        public async Task ObservableCounterOverflowSumsAcrossFoldedSeries()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");

            // The listener processes the returned measurements in array order, so with a limit of 1 the
            // first series ("reg") becomes the regular point and the rest fold into the overflow point.
            meter.CreateObservableCounter<long>("test.obs.counter", () => new[]
            {
                new Measurement<long>(5, new KeyValuePair<string, object>("k", "reg")),
                new Measurement<long>(10, new KeyValuePair<string, object>("k", "o1")),
                new Measurement<long>(20, new KeyValuePair<string, object>("k", "o2")),
            });

            await pipeline.ForceCollectAndExportAsync();
            var metrics = testExporter.ExportedMetrics.Where(m => m.InstrumentName == "test.obs.counter").ToList();

            metrics.Count.Should().Be(2, "1 regular point + 1 overflow point");

            var overflow = metrics.Single(m => m.Tags.ContainsKey("otel.metric.overflow"));
            overflow.SnapshotSum.Should().Be(30.0, "the overflow point must SUM the folded observable series (10 + 20), not overwrite with the last one");

            metrics.Single(m => !m.Tags.ContainsKey("otel.metric.overflow")).SnapshotSum.Should().Be(5.0);
        }

        [Fact]
        public async Task ObservableCounterOverflowComputesDeltaAcrossCycles()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");

            // Observable counters report cumulative values; mutate the overflowed series between cycles.
            long o1 = 10;
            long o2 = 20;
            meter.CreateObservableCounter<long>("test.obs.counter", () => new[]
            {
                new Measurement<long>(5, new KeyValuePair<string, object>("k", "reg")),
                new Measurement<long>(o1, new KeyValuePair<string, object>("k", "o1")),
                new Measurement<long>(o2, new KeyValuePair<string, object>("k", "o2")),
            });

            // Cycle 1: overflow cumulative = 10 + 20 = 30; first-cycle delta from 0 is 30.
            await pipeline.ForceCollectAndExportAsync();
            var afterCycle1 = testExporter.ExportedMetrics.Count;
            testExporter.ExportedMetrics
                .Single(m => m.InstrumentName == "test.obs.counter" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotSum.Should().Be(30.0);

            // Cycle 2: overflow cumulative = 15 + 25 = 40; delta = 40 - 30 = 10.
            o1 = 15;
            o2 = 25;
            await pipeline.ForceCollectAndExportAsync();
            testExporter.ExportedMetrics.Skip(afterCycle1)
                .Single(m => m.InstrumentName == "test.obs.counter" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotSum.Should().Be(10.0, "delta temporality should report the change in the summed bucket cumulative (40 - 30)");
        }

#if NET7_0_OR_GREATER
        [Fact]
        public async Task ObservableUpDownCounterOverflowSumsCumulative()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");

            // ObservableUpDownCounter is non-monotonic and cumulative; the overflow bucket should be the
            // cumulative sum of the folded series, including negative contributions.
            meter.CreateObservableUpDownCounter<long>("test.obs.updown", () => new[]
            {
                new Measurement<long>(5, new KeyValuePair<string, object>("k", "reg")),
                new Measurement<long>(10, new KeyValuePair<string, object>("k", "o1")),
                new Measurement<long>(-3, new KeyValuePair<string, object>("k", "o2")),
            });

            await pipeline.ForceCollectAndExportAsync();

            testExporter.ExportedMetrics
                .Single(m => m.InstrumentName == "test.obs.updown" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotSum.Should().Be(7.0, "the overflow bucket should be the cumulative sum of the folded series (10 + -3)");
        }
#endif

#if NET7_0_OR_GREATER
        [Fact]
        public async Task UpDownCounterOverflowAggregatesCumulativeAcrossCycles()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var upDown = meter.CreateUpDownCounter<long>("test.updown");

            // "reg" claims the single regular slot; subsequent distinct tag sets fold into the overflow
            // point. UpDownCounter is non-monotonic, so negative contributions are valid.
            upDown.Add(100, new KeyValuePair<string, object>("k", "reg"));
            upDown.Add(10, new KeyValuePair<string, object>("k", "o1"));
            upDown.Add(-3, new KeyValuePair<string, object>("k", "o2"));

            await pipeline.ForceCollectAndExportAsync();
            var afterCycle1 = testExporter.ExportedMetrics.Count;
            testExporter.ExportedMetrics
                .Single(m => m.InstrumentName == "test.updown" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotSum.Should().Be(7.0, "overflow should sum the folded series including negatives (10 + -3)");

            // UpDownCounter uses Cumulative temporality: the overflow total is not reset between cycles,
            // so a further increment accumulates on top of the previous total.
            upDown.Add(5, new KeyValuePair<string, object>("k", "o1"));
            await pipeline.ForceCollectAndExportAsync();
            testExporter.ExportedMetrics.Skip(afterCycle1)
                .Single(m => m.InstrumentName == "test.updown" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotSum.Should().Be(12.0, "cumulative temporality keeps accumulating the overflow total across cycles (7 + 5)");
        }
#endif

        [Fact]
        public async Task ObservableGaugeOverflowUsesLastValue()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");

            // Gauges are not summable; when distinct series fold into the overflow point, last-value-wins
            // is the accepted (lossy) behavior. The listener processes measurements in array order, so the
            // last overflowed series ("o2") deterministically wins.
            meter.CreateObservableGauge<long>("test.obs.gauge", () => new[]
            {
                new Measurement<long>(5, new KeyValuePair<string, object>("k", "reg")),
                new Measurement<long>(10, new KeyValuePair<string, object>("k", "o1")),
                new Measurement<long>(20, new KeyValuePair<string, object>("k", "o2")),
            });

            await pipeline.ForceCollectAndExportAsync();
            testExporter.ExportedMetrics
                .Single(m => m.InstrumentName == "test.obs.gauge" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotGaugeValue.Should().Be(20.0, "gauge overflow keeps the last folded series value, not a sum");
        }

#if NET9_0_OR_GREATER
        [Fact]
        public async Task GaugeOverflowUsesLastValue()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsCardinalityLimit, "1" },
                });

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            var testExporter = new InMemoryExporter();
            await using var pipeline = new OtelMetricsPipeline(settings, testExporter);
            pipeline.Start();

            using var meter = new Meter("TestMeter");
            var gauge = meter.CreateGauge<long>("test.gauge");

            // "reg" claims the regular slot; the remaining records fold into the overflow point, where
            // last-value-wins applies (gauges are not summable).
            gauge.Record(5, new KeyValuePair<string, object>("k", "reg"));
            gauge.Record(10, new KeyValuePair<string, object>("k", "o1"));
            gauge.Record(20, new KeyValuePair<string, object>("k", "o2"));

            await pipeline.ForceCollectAndExportAsync();
            testExporter.ExportedMetrics
                .Single(m => m.InstrumentName == "test.gauge" && m.Tags.ContainsKey("otel.metric.overflow"))
                .SnapshotGaugeValue.Should().Be(20.0, "gauge overflow keeps the last recorded value among folded series");
        }
#endif

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
