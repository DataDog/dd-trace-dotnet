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
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
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

            var mutex = new ManualResetEventSlim();

            int invocationCount = 0;

            var api = new Mock<IApi>();
            api.Setup(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), bucketDuration.ToNanoseconds()))
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
            var aggregator = new StatsAggregator(api.Object, GetSettings(bucketDurationSeconds), new StubDiscoveryService());

            try
            {
                var stopwatch = Stopwatch.StartNew();

                bool success = false;

                while (stopwatch.Elapsed.Minutes < 1)
                {
                    // Flush is not called if no spans are processed
                    aggregator.Add(new Span(new SpanContext(1, 1), DateTime.UtcNow));

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

        [Fact]
        public async Task EmptyBuckets()
        {
            var api = new Mock<IApi>();

            // First, validate that Flush does call SendStatsAsync even if disposed
            // If this behavior change then the test needs to be rewritten
            var aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService());

            // Dispose immediately to make Flush complete without delay
            await aggregator.DisposeAsync();

            aggregator.Add(new Span(new SpanContext(1, 2), DateTimeOffset.UtcNow));

            await aggregator.Flush();

            // Make sure that SendStatsAsync was called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>()), Times.Once);
            api.Reset();

            // Now the actual test
            aggregator = new StatsAggregator(api.Object, GetSettings(), new StubDiscoveryService());
            await aggregator.DisposeAsync();

            await aggregator.Flush();

            // No span is pushed so SendStatsAsync shouldn't be called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task CreatesDistinctBuckets_TS003()
        {
            const int millisecondsToNanoseconds = 1_000_000;
            const long durationMs = 100;
            const long duration = durationMs * millisecondsToNanoseconds;

            ulong id = 0;
            var start = DateTimeOffset.UtcNow;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                // Baseline
                var baselineSpan = CreateSpan(id++, start, durationMs);

                // Unique Name (Operation)
                var operationSpan = CreateSpan(id++, start, durationMs, operationName: "unique-name");

                // Unique Resource
                var resourceSpan = CreateSpan(id++, start, durationMs, resourceName: "unique-resource");

                // Unique Service
                var serviceSpan = CreateSpan(id++, start, durationMs, serviceName: "unique-service");

                // Unique Type
                var typeSpan = CreateSpan(id++, start, durationMs, type: "unique-type");

                // Unique Synthetics
                var syntheticsSpan = CreateSpan(id++, start, durationMs, origin: "synthetics");

                // Unique HTTP Status Code
                var httpSpan = CreateSpan(id++, start, durationMs, httpStatusCode: "400");

                var spans = new Span[] { baselineSpan, operationSpan, resourceSpan, serviceSpan, typeSpan, syntheticsSpan, httpSpan };
                aggregator.Add(spans);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(7);

                foreach (var span in spans)
                {
                    var key = StatsAggregator.BuildKey(span);
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

            Span CreateSpan(ulong id, DateTimeOffset start, long durationMs, string operationName = "name", string resourceName = "resource", string serviceName = "service", string type = "http", string httpStatusCode = "200", string origin = "rum")
            {
                var span = new Span(new SpanContext(id, id), start);
                span.SetDuration(TimeSpan.FromMilliseconds(durationMs));

                span.ResourceName = resourceName;
                span.ServiceName = serviceName;
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

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var parentSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                parentSpan.OperationName = "web.request";
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // childSpan shouldn't be recorded, because it's not top-level and doesn't have the Measured tag
                var childSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service"), start);
                childSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var measuredChildSpan1 = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service"), start);
                measuredChildSpan1.OperationName = "child.op1";
                measuredChildSpan1.SetTag(Tags.Measured, "1");
                measuredChildSpan1.SetDuration(TimeSpan.FromMilliseconds(100));

                var measuredChildSpan2 = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service"), start);
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

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var simpleSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                simpleSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var parentSpan = new Span(new SpanContext(2, 2, serviceName: "service"), start);
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(200));

                // snapshotSpan shouldn't be recorded, because it has the PartialSnapshot metric (even though it is top-level)
                var snapshotSpan = new Span(new SpanContext(5, 5, serviceName: "service"), start);
                snapshotSpan.SetMetric(Tags.PartialSnapshot, 1.0);
                snapshotSpan.SetDuration(TimeSpan.FromMilliseconds(300));

                // Create a new child span that is a service entry span, which means it will have stats computed for it
                var httpClientServiceSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service-http-client"), start);
                httpClientServiceSpan.SetDuration(TimeSpan.FromMilliseconds(400));

                aggregator.Add(simpleSpan, parentSpan, snapshotSpan, httpClientServiceSpan);

                var buffer = aggregator.CurrentBuffer;

                buffer.Buckets.Should().HaveCount(2);

                var serviceKey = StatsAggregator.BuildKey(simpleSpan);
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

                var httpClientServiceKey = StatsAggregator.BuildKey(httpClientServiceSpan);
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

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var success1Span = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                success1Span.SetDuration(TimeSpan.FromMilliseconds(100));

                var success2Span = new Span(new SpanContext(2, 2, serviceName: "service"), start);
                success2Span.SetDuration(TimeSpan.FromMilliseconds(200));

                var errorSpan = new Span(new SpanContext(3, 3, serviceName: "service"), start);
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

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                int sampleCount = 100;
                var durations = new double[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    var span = new Span(new SpanContext((ulong)i, (ulong)i, serviceName: "service"), start);
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

        private static ImmutableTracerSettings GetSettings(int? statsComputationIntervalSeconds = null)
        {
            var settings = statsComputationIntervalSeconds.HasValue
                               ? TracerSettings.Create(new() { { ConfigurationKeys.StatsComputationInterval, statsComputationIntervalSeconds.Value } })
                               : new TracerSettings();

            return settings.Build();
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

        private class StubDiscoveryService : IDiscoveryService
        {
            public void SubscribeToChanges(Action<AgentConfiguration> callback)
            {
                callback(new AgentConfiguration(
                             configurationEndpoint: "configurationEndpoint",
                             debuggerEndpoint: "debuggerEndpoint",
                             diagnosticsEndpoint: "diagnosticsEndpoint",
                             symbolDbEndpoint: "symbolDbEndpoint",
                             agentVersion: "agentVersion",
                             statsEndpoint: "traceStatsEndpoint",
                             dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                             eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                             telemetryProxyEndpoint: "telemetryProxyEndpoint",
                             tracerFlareEndpoint: "tracerFlareEndpoint",
                             clientDropP0: true,
                             spanMetaStructs: true));
            }

            public void RemoveSubscription(Action<AgentConfiguration> callback)
            {
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
    }
}
