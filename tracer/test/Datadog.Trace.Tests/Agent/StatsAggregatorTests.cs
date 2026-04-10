// <copyright file="StatsAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class StatsAggregatorTests
    {
        [Fact]
        public async Task CallFlushAutomatically()
        {
            const int bucketDurationSeconds = 1;
            var bucketDuration = TimeSpan.FromSeconds(bucketDurationSeconds);

            using var mutex = new ManualResetEventSlim();

            int invocationCount = 0;

            var api = new Mock<IApi>();
            api.Setup(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), bucketDuration.ToNanoseconds(), It.IsAny<int>()))
                .Callback(
                    () =>
                    {
                        if (Interlocked.Increment(ref invocationCount) == 2)
                        {
                            mutex.Set();
                        }
                    })
                .Returns(Task.FromResult(true));

            // Mock the DiscoveryService so StatsAggregator.CanComputeStats = true and Api.SendStatsAsync will be called
            var aggregator = new StatsAggregator(api.Object, GetSettings(bucketDurationSeconds), new StubDiscoveryService(), isOtlp: false);

            try
            {
                var stopwatch = Stopwatch.StartNew();

                bool success = false;

                while (stopwatch.Elapsed.Minutes < 1)
                {
                    // Flush is not called if no spans are processed
                    aggregator.Add(CreateTopLevelSpan(DateTime.UtcNow));

                    if (mutex.Wait(TimeSpan.FromMilliseconds(100)))
                    {
                        success = true;
                        break;
                    }
                }

                success.Should().BeTrue();
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("7.65.0", true)]
        [InlineData("7.66.0", true)]
        [InlineData("8.0.0", true)]
        [InlineData("7.64.0", false)]
        [InlineData("7.64.99", false)]
        [InlineData("7.0.0", false)]
        [InlineData("not-a-version", true)]  // Unparseable versions don't block (e.g. test agents)
        [InlineData(null, true)]              // Null version doesn't block
        public async Task StatsComputation_RequiresMinimumAgentVersion(string agentVersion, bool expectedEnabled)
        {
            var api = new Mock<IApi>();
            var discovery = new StubDiscoveryService(agentVersion);
            var aggregator = new StatsAggregator(api.Object, GetSettings(), discovery, isOtlp: false);

            try
            {
                aggregator.CanComputeStats.Should().Be(expectedEnabled);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task EmptyBuckets()
        {
            var api = new Mock<IApi>();

            // First, validate that Flush does call SendStatsAsync even if disposed
            // If this behavior change then the test needs to be rewritten
            var aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService(), isOtlp: false);

            // Dispose immediately to make Flush complete without delay
            await aggregator.DisposeAsync();

            aggregator.Add(CreateTopLevelSpan(DateTimeOffset.UtcNow));

            await aggregator.Flush();

            // Make sure that SendStatsAsync was called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>(), It.IsAny<int>()), Times.Once);
            api.Reset();

            // Now the actual test
            aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService(), isOtlp: false);
            await aggregator.DisposeAsync();

            await aggregator.Flush();

            // No span is pushed so SendStatsAsync shouldn't be called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task StaleBuckets_DoNotTriggerFlush()
        {
            var api = new Mock<IApi>();
            var aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService(), isOtlp: false);
            await aggregator.DisposeAsync();

            // Add a span and flush — this should send stats
            aggregator.Add(CreateTopLevelSpan(DateTimeOffset.UtcNow));
            await aggregator.Flush();
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>(), It.IsAny<int>()), Times.Once);
            api.Reset();

            // Flush again with no new spans. The buffer still has stale keys (retained
            // for DDSketch reuse) but with Hits == 0. This should NOT trigger a send,
            // otherwise the agent receives an empty stats array.
            await aggregator.Flush();
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task IdleFlush_StillResetsStartTimestamp()
        {
            var api = new Mock<IApi>();
            var aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService(), isOtlp: false);
            await aggregator.DisposeAsync();

            // Add a span and flush to populate the buffer
            aggregator.Add(CreateTopLevelSpan(DateTimeOffset.UtcNow));
            await aggregator.Flush();
            api.Reset();

            // Record the Start timestamp after the first flush (buffer was swapped, so
            // the "current" buffer is the one that will receive new spans next).
            var buffer = aggregator.CurrentBuffer;
            var startAfterFirstFlush = buffer.Start;

            // Flush with no new spans — no API call, but Reset must still run to
            // re-align Start and prune stale keys.
            await aggregator.Flush();

            // The buffer has been swapped again; grab the one that was just flushed.
            // After the second flush, the buffer that was "current" has been reset.
            // Its Start should have been updated (re-aligned to the current 10s boundary).
            // Because at least a few nanoseconds have elapsed, or at the very least the
            // stale keys from the first flush should have been pruned.
            buffer.Start.Should().BeGreaterOrEqualTo(startAfterFirstFlush);
            buffer.Buckets.Should().BeEmpty("stale keys with zero hits should be pruned by Reset");
        }

        [Fact]
        public async Task CreatesDistinctBuckets_TS003()
        {
            const int millisecondsToNanoseconds = 1_000_000;
            const long durationMs = 100;
            const long duration = durationMs * millisecondsToNanoseconds;

            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                // Baseline
                var baselineSpan = CreateSpan(start, durationMs);

                // Unique Name (Operation)
                var operationSpan = CreateSpan(start, durationMs, operationName: "unique-name");

                // Unique Resource
                var resourceSpan = CreateSpan(start, durationMs, resourceName: "unique-resource");

                // Unique Service
                var serviceSpan = CreateSpan(start, durationMs, serviceName: "unique-service");

                // Unique Type
                var typeSpan = CreateSpan(start, durationMs, type: "unique-type");

                // Unique Synthetics
                var syntheticsSpan = CreateSpan(start, durationMs, origin: "synthetics");

                // Unique HTTP Status Code
                var httpSpan = CreateSpan(start, durationMs, httpStatusCode: "400");

                var spans = new Span[] { baselineSpan, operationSpan, resourceSpan, serviceSpan, typeSpan, syntheticsSpan, httpSpan };
                aggregator.Add(spans);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(7);

                foreach (var span in spans)
                {
                    var key = aggregator.BuildKey(span, out _);
                    buffer.Buckets.Should().ContainKey(key);

                    var bucket = buffer.Buckets[key];
                    bucket.Duration.Should().Be(duration);
                    bucket.Hits.Should().Be(1);
                    bucket.Errors.Should().Be(0);
                    bucket.TopLevelHits.Should().Be(1);
                    bucket.ErrorSummary.GetCount().Should().Be(0);
                    bucket.ErrorSummary.GetSum().Should().Be(0);
                    bucket.OkSummary.GetCount().Should().Be(1.0);
                    bucket.OkSummary.GetSum().Should().BeApproximately(
                        duration, duration * bucket.OkSummary.IndexMapping.RelativeAccuracy);
                }
            }
            finally
            {
                await aggregator.DisposeAsync();
            }

            Span CreateSpan(DateTimeOffset start, long durationMs, string operationName = "name", string resourceName = "resource", string serviceName = "service", string type = "http", string httpStatusCode = "200", string origin = "rum")
            {
                var span = CreateTopLevelSpan(start, serviceName);
                span.SetDuration(TimeSpan.FromMilliseconds(durationMs));

                span.ResourceName = resourceName;
                span.OperationName = operationName;
                span.Type = type;
                span.SetTag(Tags.HttpStatusCode, httpStatusCode);
                span.Context.Origin = origin;

                return span;
            }
        }

        [Fact]
        public async Task CollectsMeasuredSpans_TS004()
        {
            const int millisecondsToNanoseconds = 1_000_000;

            // All spans should be recorded except childSpan
            const long expectedTotalDuration = 100 * millisecondsToNanoseconds;
            const long expectedOkDuration = 100 * millisecondsToNanoseconds;

            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var parentSpan = CreateTopLevelSpan(start, "service");
                parentSpan.OperationName = "web.request";
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // childSpan shouldn't be recorded, because it's not top-level and doesn't have the Measured tag
                var childSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                childSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var measuredChildSpan1 = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                measuredChildSpan1.OperationName = "child.op1";
                measuredChildSpan1.SetTag(Tags.Measured, "1");
                measuredChildSpan1.SetDuration(TimeSpan.FromMilliseconds(100));

                var measuredChildSpan2 = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                measuredChildSpan2.OperationName = "child.op2";
                measuredChildSpan2.SetTag(Tags.Measured, "1");
                measuredChildSpan2.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(parentSpan, childSpan, measuredChildSpan1, measuredChildSpan2);

                var buffer = aggregator.CurrentBuffer;

                buffer.Buckets.Should().HaveCount(3);

                foreach (var bucket in buffer.Buckets.Values)
                {
                    var topLevelHits = bucket.Key.OperationName == "web.request" ? 1 : 0;

                    bucket.Duration.Should().Be(expectedTotalDuration);
                    bucket.Hits.Should().Be(1);
                    bucket.Errors.Should().Be(0);
                    bucket.TopLevelHits.Should().Be(topLevelHits);
                    bucket.ErrorSummary.GetCount().Should().Be(0);
                    bucket.ErrorSummary.GetSum().Should().Be(0);
                    bucket.OkSummary.GetCount().Should().Be(1.0);
                    bucket.OkSummary.GetSum().Should().BeApproximately(
                        expectedOkDuration, expectedOkDuration * bucket.OkSummary.IndexMapping.RelativeAccuracy);
                }
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task CollectsTopLevelSpans_TS005()
        {
            const int millisecondsToNanoseconds = 1_000_000;

            // All spans should be recorded except snapshotSpan
            const long expectedTotalDuration = (100 + 200) * millisecondsToNanoseconds;
            const long expectedOkDuration = (100 + 200) * millisecondsToNanoseconds;

            const long expectedHttpClientTotalDuration = 400 * millisecondsToNanoseconds;
            const long expectedHttpClientOkDuration = 400 * millisecondsToNanoseconds;

            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var simpleSpan = CreateTopLevelSpan(start, "service");
                simpleSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var parentSpan = CreateTopLevelSpan(start, "service");
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(200));

                // snapshotSpan shouldn't be recorded, because it has the PartialSnapshot metric (even though it is top-level)
                var snapshotSpan = CreateTopLevelSpan(start, "service");
                snapshotSpan.SetMetric(Tags.PartialSnapshot, 1.0);
                snapshotSpan.SetDuration(TimeSpan.FromMilliseconds(300));

                // Create a new child span that is a service entry span, which means it will have stats computed for it
                var httpClientServiceSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service-http-client"), start);
                httpClientServiceSpan.SetDuration(TimeSpan.FromMilliseconds(400));

                aggregator.Add(simpleSpan, parentSpan, snapshotSpan, httpClientServiceSpan);

                var buffer = aggregator.CurrentBuffer;

                buffer.Buckets.Should().HaveCount(2);

                var serviceKey = aggregator.BuildKey(simpleSpan, out _);
                buffer.Buckets.Should().ContainKey(serviceKey);
                var serviceBucket = buffer.Buckets[serviceKey];

                serviceBucket.Duration.Should().Be(expectedTotalDuration);
                serviceBucket.Hits.Should().Be(2);
                serviceBucket.Errors.Should().Be(0);
                serviceBucket.TopLevelHits.Should().Be(2);
                serviceBucket.ErrorSummary.GetCount().Should().Be(0);
                serviceBucket.ErrorSummary.GetSum().Should().Be(0);
                serviceBucket.OkSummary.GetCount().Should().Be(2.0);
                serviceBucket.OkSummary.GetSum().Should().BeApproximately(
                    expectedOkDuration, expectedOkDuration * serviceBucket.OkSummary.IndexMapping.RelativeAccuracy);

                var httpClientServiceKey = aggregator.BuildKey(httpClientServiceSpan, out _);
                buffer.Buckets.Should().ContainKey(httpClientServiceKey);
                var httpClientServiceBucket = buffer.Buckets[httpClientServiceKey];

                httpClientServiceBucket.Duration.Should().Be(expectedHttpClientTotalDuration);
                httpClientServiceBucket.Hits.Should().Be(1);
                httpClientServiceBucket.Errors.Should().Be(0);
                httpClientServiceBucket.TopLevelHits.Should().Be(1);
                httpClientServiceBucket.ErrorSummary.GetCount().Should().Be(0);
                httpClientServiceBucket.ErrorSummary.GetSum().Should().Be(0);
                httpClientServiceBucket.OkSummary.GetCount().Should().Be(1.0);
                httpClientServiceBucket.OkSummary.GetSum().Should().BeApproximately(
                    expectedHttpClientOkDuration, expectedHttpClientOkDuration * httpClientServiceBucket.OkSummary.IndexMapping.RelativeAccuracy);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task RecordsSuccessesAndErrorsSeparately_TS006()
        {
            const int millisecondsToNanoseconds = 1_000_000;

            const long expectedTotalDuration = (100 + 200 + 400) * millisecondsToNanoseconds;
            const long expectedOkDuration = (100 + 200) * millisecondsToNanoseconds;
            const long expectedErrorDuration = 400 * millisecondsToNanoseconds;

            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var success1Span = CreateTopLevelSpan(start, "service");
                success1Span.SetDuration(TimeSpan.FromMilliseconds(100));

                var success2Span = CreateTopLevelSpan(start, "service");
                success2Span.SetDuration(TimeSpan.FromMilliseconds(200));

                var errorSpan = CreateTopLevelSpan(start, "service");
                errorSpan.Error = true;
                errorSpan.SetDuration(TimeSpan.FromMilliseconds(400));

                aggregator.Add(success1Span, success2Span, errorSpan);

                var buffer = aggregator.CurrentBuffer;

                buffer.Buckets.Should().HaveCount(1);
                var bucket = buffer.Buckets.Values.Single();

                bucket.Duration.Should().Be(expectedTotalDuration);
                bucket.Hits.Should().Be(3);
                bucket.Errors.Should().Be(1);
                bucket.TopLevelHits.Should().Be(3);
                bucket.ErrorSummary.GetCount().Should().Be(1.0);
                bucket.ErrorSummary.GetSum().Should().BeApproximately(
                    expectedErrorDuration, expectedErrorDuration * bucket.ErrorSummary.IndexMapping.RelativeAccuracy);
                bucket.OkSummary.GetCount().Should().Be(2.0);
                bucket.OkSummary.GetSum().Should().BeApproximately(
                    expectedOkDuration, expectedOkDuration * bucket.OkSummary.IndexMapping.RelativeAccuracy);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task RelativeErrorIsAccurate_TS009()
        {
            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                int sampleCount = 100;
                var durations = new double[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    var span = CreateTopLevelSpan(start, "service");
                    var duration = TimeSpan.FromMilliseconds(i * 100);

                    span.SetDuration(duration);
                    durations[i] = ConvertTimestamp(duration.ToNanoseconds());
                    aggregator.Add(span);
                }

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(1);
                var bucket = buffer.Buckets.Values.Single();

                // Sort the durations so we can grab the actual sample that corresponds with a quantile
                Array.Sort(durations);
                double[] quantiles = new double[] { 0.5, 0.75, 0.95, 0.99, 1 };

                foreach (var quantile in quantiles)
                {
                    var actualQuantileValue = durations[GetQuantileIndex(quantile, sampleCount)];
                    var ddSketchQuantileValue = bucket.OkSummary.GetValueAtQuantile(quantile);
                    ddSketchQuantileValue.Should().BeApproximately(actualQuantileValue, actualQuantileValue * bucket.OkSummary.IndexMapping.RelativeAccuracy, "We expect quantiles to be accurate at quantile {0}", quantile);
                }
            }
            finally
            {
                await aggregator.DisposeAsync();
            }

            // Accepted values of quantile are 0 <= q <= 1
            // If q = 0, returns 0
            // If q = 1, returns arrayLength - 1
            static int GetQuantileIndex(double quantile, int arrayLength)
            {
                var numberOfElementsLessThanOrEqualTo = (int)Math.Floor(1 + (quantile * (arrayLength - 1)));
                return numberOfElementsLessThanOrEqualTo - 1;
            }
        }

        [Fact]
        public void CreateStatsAggregator_Otlp_AlwaysComputesStats()
        {
            var aggregator = StatsAggregator.Create(Mock.Of<IApi>(), GetSettings(), NullDiscoveryService.Instance, isOtlp: true);
            aggregator.CanComputeStats.Should().BeTrue();
        }

        [Fact]
        public async Task Otlp_ProcessTrace_WhenTraceSampled()
        {
            var aggregator = StatsAggregator.Create(Mock.Of<IApi>(), GetSettings(), NullDiscoveryService.Instance, isOtlp: true);
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.AutoKeep, mechanism: SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            var dropReason = aggregator.ProcessTrace(ref traceChunk);
            dropReason.Should().Be(TraceKeepState.Keep);
        }

        [Fact]
        public async Task Otlp_ProcessTrace_WhenTraceNotSampled()
        {
            var aggregator = StatsAggregator.Create(Mock.Of<IApi>(), GetSettings(), NullDiscoveryService.Instance, isOtlp: true);
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.AutoReject, mechanism: SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            var dropReason = aggregator.ProcessTrace(ref traceChunk);
            dropReason.Should().Be(TraceKeepState.DropUnsampled);
        }

        [Fact]
        public async Task ProcessTrace_WhenSampled_ReturnsKeep()
        {
            var discoveryService = new StubDiscoveryService(obfuscationVersion: 1);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.Type = "sql";
            span.ResourceName = "SELECT * FROM users WHERE id = 123";
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);
            span.SetService(string.Empty, "manual");
            span.Finish();

            span.ServiceName.Should().BeEmpty();
            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ProcessTrace(ref traceChunk);
            result.Should().Be(TraceKeepState.Keep);
            traceChunk.Count.Should().Be(1);
            // normalized
            span.ServiceName.Should().NotBeEmpty();
            // obfuscated
            traceChunk[0].ResourceName.Should().NotBe("SELECT * FROM users WHERE id = 123");
        }

        [Fact]
        public async Task ProcessTrace_WhenFilterRejects_ReturnsTraceFilter()
        {
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: ["env:production"],
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: null);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.SetTag("env", "production");
            traceContext.AddSpan(span);

            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ProcessTrace(ref traceChunk);
            result.Should().Be(TraceKeepState.TraceFilter);
        }

        [Fact]
        public async Task ProcessTrace_WhenFilterKeepsAndSampled_ReturnsKeep()
        {
            // Configure a reject filter that does NOT match → trace passes filter, then goes to sampling
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: ["env:staging"],
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: null);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.SetTag("env", "production");
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ProcessTrace(ref traceChunk);
            result.Should().Be(TraceKeepState.Keep);
        }

        [Fact]
        public async Task ProcessTrace_WhenFilterKeepsAndNotSampled_ReturnsDropUnsampled()
        {
            // Configure a reject filter that does NOT match → trace passes filter, then goes to sampling
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: ["env:staging"],
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: null);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.SetTag("env", "production");
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ProcessTrace(ref traceChunk);
            result.Should().Be(TraceKeepState.DropUnsampled);
        }

        [Fact]
        public async Task ShouldFilterTrace_WhenNoFilter_ReturnsFalse()
        {
            // No discovery service → no trace filter configured
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            var span = CreateTopLevelSpan(DateTimeOffset.UtcNow, "service");
            span.OperationName = "operation";
            span.SetDuration(TimeSpan.FromMilliseconds(100));

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldFilterTrace(in traceChunk).Should().BeFalse();
        }

        [Fact]
        public async Task ShouldFilterTrace_WhenFilterKeepsTrace_ReturnsFalse()
        {
            // Configure a reject filter that does NOT match the span → trace should be kept (not filtered)
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: ["env:staging"],
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: null);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.SetTag("env", "production");
            traceContext.AddSpan(span);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldFilterTrace(in traceChunk).Should().BeFalse();
        }

        [Fact]
        public async Task ShouldFilterTrace_WhenFilterRejectsTrace_ReturnsTrue()
        {
            // Configure a reject filter that matches the span → trace should be filtered
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: ["env:production"],
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: null);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            span.SetTag("env", "production");
            traceContext.AddSpan(span);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldFilterTrace(in traceChunk).Should().BeTrue();
        }

        [Fact]
        public async Task ShouldFilterTrace_WhenIgnoreResourceMatches_ReturnsTrue()
        {
            var filterConfig = new AgentTraceFilterConfig(
                FilterTagsRequire: null,
                FilterTagsReject: null,
                FilterTagsRegexRequire: null,
                FilterTagsRegexReject: null,
                IgnoreResources: ["^GET /health"]);

            var discoveryService = new StubDiscoveryService(traceFilterConfig: filterConfig);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "http.request" };
            span.ResourceName = "GET /healthcheck";
            traceContext.AddSpan(span);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldFilterTrace(in traceChunk).Should().BeTrue();
        }

        [Fact]
        public async Task ShouldKeepTrace_WhenPrioritySampled_ReturnsTrue()
        {
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldKeepTrace(in traceChunk).Should().BeTrue();
        }

        [Fact]
        public async Task ShouldKeepTrace_WhenNotSampled_ReturnsFalse()
        {
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoReject, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldKeepTrace(in traceChunk).Should().BeFalse();
        }

        [Fact]
        public async Task ShouldKeepTrace_Otlp_WhenSampled_ReturnsTrue()
        {
            await using var aggregator = (StatsAggregator)StatsAggregator.Create(Mock.Of<IApi>(), GetSettings(), NullDiscoveryService.Instance, isOtlp: true);

            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.LocalTraceSamplingRule, rate: null, limiterRate: null);

            var traceChunk = new SpanCollection([span]);
            aggregator.ShouldKeepTrace(in traceChunk).Should().BeTrue();
        }

        [Fact]
        public async Task ObfuscateTrace_WhenNotEnabled_ReturnsUnmodified()
        {
            // Default: obfuscation version is 0 (not enabled), so ObfuscateTrace should return the same collection
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            var span = CreateTopLevelSpan(DateTimeOffset.UtcNow, "service");
            span.OperationName = "operation";
            span.SetDuration(TimeSpan.FromMilliseconds(100));

            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ObfuscateTrace(in traceChunk);
            result.Count.Should().Be(1);
        }

        [Fact]
        public async Task ObfuscateTrace_WhenEnabled_RunsObfuscation()
        {
            // Use StubDiscoveryService with obfuscation version 1 to enable tracer obfuscation
            var discoveryService = new StubDiscoveryService(obfuscationVersion: 1);
            await using var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), discoveryService, isOtlp: false);

            var span = CreateTopLevelSpan(DateTimeOffset.UtcNow, "service");
            span.OperationName = "operation";
            span.Type = "sql";
            span.ResourceName = "SELECT * FROM users WHERE id = 123";
            span.SetDuration(TimeSpan.FromMilliseconds(100));

            var traceChunk = new SpanCollection([span]);
            var result = aggregator.ObfuscateTrace(in traceChunk);

            // Obfuscation should run and not throw; it should return a valid collection
            result.Count.Should().Be(1);
            // The SQL should have been obfuscated (numeric literal replaced)
            result[0].ResourceName.Should().NotBe("SELECT * FROM users WHERE id = 123");
        }

        [Fact]
        public async Task SpanKindEligibility_ServerAndClientSpansAreIncluded()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var parentSpan = CreateTopLevelSpan(start, "service");
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Child span with span.kind = "server" — should be included even though not top-level
                var serverChildSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                serverChildSpan.SetTag(Tags.SpanKind, SpanKinds.Server);
                serverChildSpan.OperationName = "server.child";
                serverChildSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Child span with span.kind = "client" — should be included
                var clientChildSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                clientChildSpan.SetTag(Tags.SpanKind, SpanKinds.Client);
                clientChildSpan.OperationName = "client.child";
                clientChildSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Child span with span.kind = "internal" — should NOT be included
                var internalChildSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                internalChildSpan.SetTag(Tags.SpanKind, SpanKinds.Internal);
                internalChildSpan.OperationName = "internal.child";
                internalChildSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Child span with no span.kind — should NOT be included
                var noKindChildSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
                noKindChildSpan.OperationName = "nokind.child";
                noKindChildSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(parentSpan, serverChildSpan, clientChildSpan, internalChildSpan, noKindChildSpan);

                var buffer = aggregator.CurrentBuffer;

                // parent, server child, client child are included; internal and no-kind are not
                buffer.Buckets.Should().HaveCount(3);
                buffer.Buckets.Should().ContainKey(aggregator.BuildKey(parentSpan, out _));
                buffer.Buckets.Should().ContainKey(aggregator.BuildKey(serverChildSpan, out _));
                buffer.Buckets.Should().ContainKey(aggregator.BuildKey(clientChildSpan, out _));
                buffer.Buckets.Should().NotContainKey(aggregator.BuildKey(internalChildSpan, out _));
                buffer.Buckets.Should().NotContainKey(aggregator.BuildKey(noKindChildSpan, out _));
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task SpanKindCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                // Two top-level spans identical except span.kind
                var clientSpan = CreateTopLevelSpan(start, "service");
                clientSpan.OperationName = "op";
                clientSpan.SetTag(Tags.SpanKind, SpanKinds.Client);
                clientSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var serverSpan = CreateTopLevelSpan(start, "service");
                serverSpan.OperationName = "op";
                serverSpan.SetTag(Tags.SpanKind, SpanKinds.Server);
                serverSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(clientSpan, serverSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task IsTraceRootCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                // Span A: no parent (IsTraceRoot = true)
                var rootSpan = CreateTopLevelSpan(start, "svc");
                rootSpan.OperationName = "op";
                rootSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Span B: has a parent from a different trace (IsTraceRoot = false, but IsTopLevel = true via service boundary)
                var upstreamContext = new SpanContext(traceId: 2, spanId: 999, serviceName: "upstream-svc");
                var entrySpan = new Span(new SpanContext(upstreamContext, new TraceContext(new StubDatadogTracer()), "svc"), start);
                entrySpan.OperationName = "op";
                entrySpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(rootSpan, entrySpan);

                var buffer = aggregator.CurrentBuffer;
                // They have the same resource/operation/type but different IsTraceRoot → 2 buckets
                buffer.Buckets.Should().HaveCount(2);

                var rootKey = aggregator.BuildKey(rootSpan, out _);
                var entryKey = aggregator.BuildKey(entrySpan, out _);
                rootKey.IsTraceRoot.Should().BeTrue();
                entryKey.IsTraceRoot.Should().BeFalse();
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task HttpMethodCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var getSpan = CreateTopLevelSpan(start, "svc");
                getSpan.OperationName = "http.request";
                getSpan.SetTag(Tags.HttpMethod, "GET");
                getSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var postSpan = CreateTopLevelSpan(start, "svc");
                postSpan.OperationName = "http.request";
                postSpan.SetTag(Tags.HttpMethod, "POST");
                postSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(getSpan, postSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task HttpEndpointCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var usersSpan = CreateTopLevelSpan(start, "svc");
                usersSpan.OperationName = "http.request";
                usersSpan.SetTag(Tags.HttpRoute, "/users/{id}");
                usersSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var ordersSpan = CreateTopLevelSpan(start, "svc");
                ordersSpan.OperationName = "http.request";
                ordersSpan.SetTag(Tags.HttpRoute, "/orders/{id}");
                ordersSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(usersSpan, ordersSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task GrpcStatusCodeCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var okSpan = CreateTopLevelSpan(start, "svc");
                okSpan.OperationName = "grpc.call";
                okSpan.SetTag(Tags.GrpcStatusCode, "0");
                okSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var errorSpan = CreateTopLevelSpan(start, "svc");
                errorSpan.OperationName = "grpc.call";
                errorSpan.SetTag(Tags.GrpcStatusCode, "2");
                errorSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(okSpan, errorSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("rpc.grpc.status_code", "5")]
        [InlineData("grpc.code", "5")]
        [InlineData("rpc.grpc.status.code", "5")]
        [InlineData("grpc.status.code", "5")]
        public async Task GrpcStatusCodeFallbackTags(string tagName, string tagValue)
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var span = CreateTopLevelSpan(start, "svc");
                span.OperationName = "grpc.call";
                span.SetTag(tagName, tagValue);
                span.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(span);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(1);
                var key = buffer.Buckets.Keys.First();
                key.GrpcStatusCode.Should().Be("5");
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task GrpcStatusCodePriorityOrder()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                // When multiple gRPC tags are present, the highest priority one wins
                var span = CreateTopLevelSpan(start, "svc");
                span.OperationName = "grpc.call";
                span.SetTag("rpc.grpc.status_code", "1");
                span.SetTag("grpc.status.code", "2");
                span.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(span);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(1);
                var key = buffer.Buckets.Keys.First();
                key.GrpcStatusCode.Should().Be("1");
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task ServiceSourceCreatesDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                var span1 = CreateTopLevelSpan(start, "svc");
                span1.OperationName = "op";
                span1.Context.ServiceNameSource = "integration";
                span1.SetDuration(TimeSpan.FromMilliseconds(100));

                var span2 = CreateTopLevelSpan(start, "svc");
                span2.OperationName = "op";
                span2.Context.ServiceNameSource = "user";
                span2.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(span1, span2);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task SamplingWeightIsApplied()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>(), isOtlp: false);

            try
            {
                // Span with sampling rate 0.1 → weight = 1/0.1 = 10
                // GetWeight uses TraceContext.AppliedSamplingRate
                var sampledTraceContext = new TraceContext(new StubDatadogTracer());
                sampledTraceContext.AppliedSamplingRate = 0.1f;
                var sampledSpan = new Span(new SpanContext(null, sampledTraceContext, "svc"), start);
                sampledSpan.OperationName = "op";
                sampledSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Span with no sampling rate → weight = 1.0
                var unweightedSpan = CreateTopLevelSpan(start, "svc2");
                unweightedSpan.OperationName = "op";
                unweightedSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(sampledSpan, unweightedSpan);

                var buffer = aggregator.CurrentBuffer;
                var sampledKey = aggregator.BuildKey(sampledSpan, out _);
                var unweightedKey = aggregator.BuildKey(unweightedSpan, out _);

                buffer.Buckets[sampledKey].Hits.Should().BeApproximately(10.0, 0.001);
                buffer.Buckets[unweightedKey].Hits.Should().BeApproximately(1.0, 0.001);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task PeerTagsCreateDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), new StubDiscoveryService(), isOtlp: false);

            try
            {
                // Two client spans with same resource but different peer.service values
                // Use Tags.SetTag directly to avoid PeerService special handling in Span.SetTag that requires TraceContext
                var span1 = CreateTopLevelSpan(start, "svc");
                span1.OperationName = "http.client";
                span1.SetTag(Tags.SpanKind, SpanKinds.Client);
                span1.Tags.SetTag(Tags.PeerService, "service-a");
                span1.SetDuration(TimeSpan.FromMilliseconds(100));

                var span2 = CreateTopLevelSpan(start, "svc");
                span2.OperationName = "http.client";
                span2.SetTag(Tags.SpanKind, SpanKinds.Client);
                span2.Tags.SetTag(Tags.PeerService, "service-b");
                span2.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(span1, span2);

                var buffer = aggregator.CurrentBuffer;
                // Different peer.service → 2 distinct buckets
                buffer.Buckets.Should().HaveCount(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        /// <summary>
        /// Creates a top-level span with a TraceContext (required by GetWeight).
        /// </summary>
        private static Span CreateTopLevelSpan(DateTimeOffset start, string serviceName = null)
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            var context = new SpanContext(null, traceContext, serviceName);
            return new Span(context, start);
        }

        private static TracerSettings GetSettings(int? statsComputationIntervalSeconds = null)
        {
            var settings = statsComputationIntervalSeconds.HasValue
                               ? TracerSettings.Create(new() { { ConfigurationKeys.StatsComputationInterval, statsComputationIntervalSeconds.Value } })
                               : new TracerSettings();

            return settings;
        }

        // Re-implement timestamp conversion to independently verify the operation
        private static double ConvertTimestamp(long ns)
        {
            // 10 bits precision (any value will be +/- 1/1024)
            const long roundMask = 1 << 10;

            int shift = 0;

            while (ns > roundMask)
            {
                ns >>= 1;
                shift++;
            }

            return ns << shift;
        }

        private class StubDiscoveryService(
            string agentVersion = "7.65.0",
            int obfuscationVersion = 0,
            AgentTraceFilterConfig traceFilterConfig = null) : IDiscoveryService
        {
            public void SubscribeToChanges(Action<AgentConfiguration> callback)
            {
                callback(new AgentConfiguration(
                             configurationEndpoint: "configurationEndpoint",
                             debuggerEndpoint: "debuggerEndpoint",
                             debuggerV2Endpoint: "debuggerV2Endpoint",
                             diagnosticsEndpoint: "diagnosticsEndpoint",
                             symbolDbEndpoint: "symbolDbEndpoint",
                             agentVersion: agentVersion,
                             statsEndpoint: "traceStatsEndpoint",
                             dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                             eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                             telemetryProxyEndpoint: "telemetryProxyEndpoint",
                             tracerFlareEndpoint: "tracerFlareEndpoint",
                             containerTagsHash: "containerTagsHash",
                             clientDropP0: true,
                             spanMetaStructs: true,
                             spanEvents: true,
                             peerTags: [Tags.PeerService],
                             obfuscationVersion: obfuscationVersion,
                             traceFilterConfig: traceFilterConfig));
            }

            public void RemoveSubscription(Action<AgentConfiguration> callback)
            {
            }

            public Task DisposeAsync() => Task.CompletedTask;

            public void SetCurrentConfigStateHash(string configStateHash)
            {
            }
        }
    }
}
