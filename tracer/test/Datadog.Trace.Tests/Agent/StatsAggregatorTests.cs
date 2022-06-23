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
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class StatsAggregatorTests
    {
        public static IEnumerable<object[]> GetCreateDistinctSpansDelegates()
        {
            yield return new object[] { (Delegate)SetDistinctResourceNames };
            yield return new object[] { (Delegate)SetDistinctServiceNames };
            yield return new object[] { (Delegate)SetDistinctOperationNames };
            yield return new object[] { (Delegate)SetDistinctTypes };
            yield return new object[] { (Delegate)SetDistinctHttpStatusCodes };
            yield return new object[] { (Delegate)SetDistinctSyntheticValues };
        }

        [Fact]
        public async Task CallFlushAutomatically()
        {
            var bucketDuration = TimeSpan.FromSeconds(1);

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

            var aggregator = new StatsAggregator(api.Object, GetSettings(), bucketDuration);

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
            var aggregator = new StatsAggregator(api.Object, GetSettings(), Timeout.InfiniteTimeSpan);

            // Dispose immediately to make Flush complete without delay
            await aggregator.DisposeAsync();

            aggregator.Add(new Span(new SpanContext(1, 2), DateTimeOffset.UtcNow));

            await aggregator.Flush();

            // Make sure that SendStatsAsync was called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>()), Times.Once);
            api.Reset();

            // Now the actual test
            aggregator = new StatsAggregator(api.Object, GetSettings(), Timeout.InfiniteTimeSpan);
            await aggregator.DisposeAsync();

            await aggregator.Flush();

            // No span is pushed so SendStatsAsync shouldn't be called
            api.Verify(a => a.SendStatsAsync(It.IsAny<StatsBuffer>(), It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task OnlyCollectsTopLevelSpansAndMeasuredSpans()
        {
            const int millisecondsToNanoseconds = 1_000_000;

            // All spans should be recorded except childSpan and snapshotSpan
            const long expectedTotalDuration = (100 + 200 + 300 + 500) * millisecondsToNanoseconds;
            const long expectedOkDuration = (100 + 300 + 500) * millisecondsToNanoseconds;
            const long expectedErrorDuration = 200 * millisecondsToNanoseconds;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Timeout.InfiniteTimeSpan);

            try
            {
                var start = DateTimeOffset.UtcNow;

                var simpleSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                simpleSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var errorSpan = new Span(new SpanContext(2, 2, serviceName: "service"), start);
                errorSpan.Error = true;
                errorSpan.SetDuration(TimeSpan.FromMilliseconds(200));

                var parentSpan = new Span(new SpanContext(3, 3, serviceName: "service"), start);
                parentSpan.SetDuration(TimeSpan.FromMilliseconds(300));

                // childSpan shouldn't be recorded, because it's not top-level and doesn't have the Measured tag
                var childSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service"), start);
                childSpan.SetDuration(TimeSpan.FromMilliseconds(400));

                var measuredChildSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(Mock.Of<IDatadogTracer>()), "service"), start);
                measuredChildSpan.SetTag(Tags.Measured, "1");
                measuredChildSpan.SetDuration(TimeSpan.FromMilliseconds(500));

                // snapshotSpan shouldn't be recorded, because it has the PartialSnapshot metric (even though it is top-level)
                var snapshotSpan = new Span(new SpanContext(4, 4, serviceName: "service"), start);
                snapshotSpan.SetMetric(Tags.PartialSnapshot, 1.0);
                snapshotSpan.SetDuration(TimeSpan.FromMilliseconds(600));

                aggregator.Add(simpleSpan, errorSpan, parentSpan, childSpan, measuredChildSpan, snapshotSpan);

                var buffer = aggregator.CurrentBuffer;

                buffer.Buckets.Should().HaveCount(1);

                var bucket = buffer.Buckets.Values.Single();

                bucket.Duration.Should().Be(expectedTotalDuration);
                bucket.Hits.Should().Be(4);
                bucket.Errors.Should().Be(1);
                bucket.TopLevelHits.Should().Be(3);
                bucket.ErrorSummary.GetCount().Should().Be(1.0);
                bucket.ErrorSummary.GetSum().Should().BeApproximately(
                    expectedErrorDuration, expectedErrorDuration * bucket.ErrorSummary.IndexMapping.RelativeAccuracy);
                bucket.OkSummary.GetCount().Should().Be(3.0);
                bucket.OkSummary.GetSum().Should().BeApproximately(
                    expectedOkDuration, expectedOkDuration * bucket.OkSummary.IndexMapping.RelativeAccuracy);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Theory]
        [MemberData(nameof(GetCreateDistinctSpansDelegates))]
        public async Task CreatesDistinctBuckets(Action<ISpan, ISpan> createDistinctSpans)
        {
            const int millisecondsToNanoseconds = 1_000_000;

            // All spans should be recorded except childSpan
            const long firstDuration = 100 * millisecondsToNanoseconds;
            const long secondDuration = 200 * millisecondsToNanoseconds;

            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Timeout.InfiniteTimeSpan);

            try
            {
                var start = DateTimeOffset.UtcNow;

                var firstSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                firstSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                var secondSpan = new Span(new SpanContext(2, 2, serviceName: "service"), start);
                secondSpan.SetDuration(TimeSpan.FromMilliseconds(200));

                createDistinctSpans(firstSpan, secondSpan);

                aggregator.Add(firstSpan, secondSpan);

                var buffer = aggregator.CurrentBuffer;

                var firstKey = StatsAggregator.BuildKey(firstSpan);
                var secondKey = StatsAggregator.BuildKey(secondSpan);

                buffer.Buckets.Should().HaveCount(2);
                buffer.Buckets.Should().ContainKey(firstKey);
                buffer.Buckets.Should().ContainKey(secondKey);

                var firstBucket = buffer.Buckets[firstKey];
                firstBucket.Duration.Should().Be(firstDuration);
                firstBucket.Hits.Should().Be(1);
                firstBucket.Errors.Should().Be(0);
                firstBucket.TopLevelHits.Should().Be(1);
                firstBucket.ErrorSummary.GetCount().Should().Be(0);
                firstBucket.ErrorSummary.GetSum().Should().Be(0);
                firstBucket.OkSummary.GetCount().Should().Be(1.0);
                firstBucket.OkSummary.GetSum().Should().BeApproximately(
                    firstDuration, firstDuration * firstBucket.OkSummary.IndexMapping.RelativeAccuracy);

                var secondBucket = buffer.Buckets[secondKey];
                secondBucket.Duration.Should().Be(secondDuration);
                secondBucket.Hits.Should().Be(1);
                secondBucket.Errors.Should().Be(0);
                secondBucket.TopLevelHits.Should().Be(1);
                secondBucket.ErrorSummary.GetCount().Should().Be(0);
                secondBucket.ErrorSummary.GetSum().Should().Be(0);
                secondBucket.OkSummary.GetCount().Should().Be(1.0);
                secondBucket.OkSummary.GetSum().Should().BeApproximately(
                    secondDuration, secondDuration * secondBucket.OkSummary.IndexMapping.RelativeAccuracy);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        private static ImmutableTracerSettings GetSettings() => new TracerSettings().Build();

        private static void SetDistinctResourceNames(ISpan s1, ISpan s2)
        {
            s1.ResourceName = "first";
            s2.ResourceName = "second";
        }

        private static void SetDistinctServiceNames(ISpan s1, ISpan s2)
        {
            s1.ServiceName = "first";
            s2.ServiceName = "second";
        }

        private static void SetDistinctOperationNames(ISpan s1, ISpan s2)
        {
            s1.OperationName = "first";
            s2.OperationName = "second";
        }

        private static void SetDistinctTypes(ISpan s1, ISpan s2)
        {
            s1.Type = "first";
            s2.Type = "second";
        }

        private static void SetDistinctHttpStatusCodes(ISpan s1, ISpan s2)
        {
            s1.SetTag(Tags.HttpStatusCode, "200");
            s2.SetTag(Tags.HttpStatusCode, "201");
        }

        private static void SetDistinctSyntheticValues(ISpan s1, ISpan s2)
        {
            ((SpanContext)s1.Context).Origin = "synthetics";
            ((SpanContext)s2.Context).Origin = "not-synthetics";
        }
    }
}
