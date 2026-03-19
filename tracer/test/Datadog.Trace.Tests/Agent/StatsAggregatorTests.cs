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
                span.SetService(serviceName, null);
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
                var httpClientServiceSpan = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service-http-client"), start);
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
        public async Task CreatesDistinctBucketsBySpanKind()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var serverSpan = CreateTopLevelSpan(1, start, 100);
                serverSpan.SetTag(Tags.SpanKind, SpanKinds.Server);

                var clientSpan = CreateTopLevelSpan(2, start, 100);
                clientSpan.SetTag(Tags.SpanKind, SpanKinds.Client);

                var noKindSpan = CreateTopLevelSpan(3, start, 100);

                aggregator.Add(serverSpan, clientSpan, noKindSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(3);

                var serverKey = StatsAggregator.BuildKey(serverSpan);
                serverKey.SpanKind.Should().Be("server");

                var clientKey = StatsAggregator.BuildKey(clientSpan);
                clientKey.SpanKind.Should().Be("client");

                var noKindKey = StatsAggregator.BuildKey(noKindSpan);
                noKindKey.SpanKind.Should().BeEmpty();
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task CreatesDistinctBucketsByIsTraceRoot()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                // Root span (no parent) => IsTraceRoot = 1
                var rootSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
                rootSpan.OperationName = "op";
                rootSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                // Child span (has parent) => IsTraceRoot = 2
                // This child is a service entry span (different service) so it is top-level
                var childSpan = new Span(new SpanContext(rootSpan.Context, new TraceContext(new StubDatadogTracer()), "other-service"), start);
                childSpan.OperationName = "op";
                childSpan.SetDuration(TimeSpan.FromMilliseconds(100));

                aggregator.Add(rootSpan, childSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);

                var rootKey = StatsAggregator.BuildKey(rootSpan);
                rootKey.IsTraceRoot.Should().Be(1);

                var childKey = StatsAggregator.BuildKey(childSpan);
                childKey.IsTraceRoot.Should().Be(2);
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("rpc.grpc.status_code", "0")]
        [InlineData("grpc.code", "1")]
        [InlineData("rpc.grpc.status.code", "2")]
        [InlineData("grpc.status.code", "13")]
        public void ExtractsGrpcStatusCodeFromCorrectTag(string tagName, string expectedValue)
        {
            var start = DateTimeOffset.UtcNow;
            var span = CreateTopLevelSpan(1, start, 100);
            span.SetTag(tagName, expectedValue);

            var key = StatsAggregator.BuildKey(span);
            key.GrpcStatusCode.Should().Be(expectedValue);
        }

        [Fact]
        public void GrpcStatusCodePriorityOrder()
        {
            var start = DateTimeOffset.UtcNow;
            var span = CreateTopLevelSpan(1, start, 100);

            // Set all four tags; the first in priority order should win
            span.SetTag("rpc.grpc.status_code", "first");
            span.SetTag("grpc.code", "second");
            span.SetTag("rpc.grpc.status.code", "third");
            span.SetTag(Tags.GrpcStatusCode, "fourth");

            var key = StatsAggregator.BuildKey(span);
            key.GrpcStatusCode.Should().Be("first");
        }

        [Fact]
        public async Task CreatesDistinctBucketsByHttpMethod()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var getSpan = CreateTopLevelSpan(1, start, 100);
                getSpan.SetTag(Tags.HttpMethod, "GET");

                var postSpan = CreateTopLevelSpan(2, start, 100);
                postSpan.SetTag(Tags.HttpMethod, "POST");

                aggregator.Add(getSpan, postSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);

                StatsAggregator.BuildKey(getSpan).HttpMethod.Should().Be("GET");
                StatsAggregator.BuildKey(postSpan).HttpMethod.Should().Be("POST");
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public async Task CreatesDistinctBucketsByHttpEndpoint()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                var endpoint1Span = CreateTopLevelSpan(1, start, 100);
                endpoint1Span.SetTag(Tags.HttpEndpoint, "/api/v1/users");

                var endpoint2Span = CreateTopLevelSpan(2, start, 100);
                endpoint2Span.SetTag(Tags.HttpEndpoint, "/api/v1/orders");

                aggregator.Add(endpoint1Span, endpoint2Span);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(2);

                StatsAggregator.BuildKey(endpoint1Span).HttpEndpoint.Should().Be("/api/v1/users");
                StatsAggregator.BuildKey(endpoint2Span).HttpEndpoint.Should().Be("/api/v1/orders");
            }
            finally
            {
                await aggregator.DisposeAsync();
            }
        }

        [Fact]
        public void PeerTagsComputedOnlyForClientProducerConsumer()
        {
            var start = DateTimeOffset.UtcNow;
            var peerTagKeys = new[] { "db.instance", "db.system" };

            // Client span => peer tags should be computed
            var clientSpan = CreateTopLevelSpan(1, start, 100);
            clientSpan.SetTag(Tags.SpanKind, SpanKinds.Client);
            clientSpan.SetTag("db.instance", "my-db");
            clientSpan.SetTag("db.system", "postgres");

            var clientKey = StatsAggregator.BuildKey(clientSpan, peerTagKeys);
            clientKey.PeerTagsHash.Should().NotBeEmpty();
            clientKey.PeerTagsHash.Should().Contain("db.instance:my-db");

            // Server span => peer tags should NOT be computed
            var serverSpan = CreateTopLevelSpan(2, start, 100);
            serverSpan.SetTag(Tags.SpanKind, SpanKinds.Server);
            serverSpan.SetTag("db.instance", "my-db");

            var serverKey = StatsAggregator.BuildKey(serverSpan, peerTagKeys);
            serverKey.PeerTagsHash.Should().BeEmpty();

            // Producer span => peer tags should be computed
            var producerSpan = CreateTopLevelSpan(3, start, 100);
            producerSpan.SetTag(Tags.SpanKind, SpanKinds.Producer);
            producerSpan.SetTag("db.instance", "my-queue");

            var producerKey = StatsAggregator.BuildKey(producerSpan, peerTagKeys);
            producerKey.PeerTagsHash.Should().NotBeEmpty();

            // Consumer span => peer tags should be computed
            var consumerSpan = CreateTopLevelSpan(4, start, 100);
            consumerSpan.SetTag(Tags.SpanKind, SpanKinds.Consumer);
            consumerSpan.SetTag("db.instance", "my-queue");

            var consumerKey = StatsAggregator.BuildKey(consumerSpan, peerTagKeys);
            consumerKey.PeerTagsHash.Should().NotBeEmpty();
        }

        [Fact]
        public void PeerTagsHashIsSortedAndConsistent()
        {
            var start = DateTimeOffset.UtcNow;
            var peerTagKeys = new[] { "z.tag", "a.tag", "m.tag" };

            var span = CreateTopLevelSpan(1, start, 100);
            span.SetTag(Tags.SpanKind, SpanKinds.Client);
            span.SetTag("z.tag", "zval");
            span.SetTag("a.tag", "aval");
            span.SetTag("m.tag", "mval");

            var key = StatsAggregator.BuildKey(span, peerTagKeys);

            // Should be sorted by key name (ordinal)
            key.PeerTagsHash.Should().Be("a.tag:aval,m.tag:mval,z.tag:zval");
        }

        [Fact]
        public void PeerTagsHashOmitsMissingKeys()
        {
            var start = DateTimeOffset.UtcNow;
            var peerTagKeys = new[] { "db.instance", "db.system", "net.peer.name" };

            var span = CreateTopLevelSpan(1, start, 100);
            span.SetTag(Tags.SpanKind, SpanKinds.Client);
            span.SetTag("db.instance", "my-db");
            // db.system and net.peer.name are not set

            var key = StatsAggregator.BuildKey(span, peerTagKeys);
            key.PeerTagsHash.Should().Be("db.instance:my-db");
        }

        [Fact]
        public void SpanKindBasedEligibility()
        {
            var start = DateTimeOffset.UtcNow;

            // Non-top-level, non-measured span with span.kind=server should be eligible
            var parentSpan = new Span(new SpanContext(1, 1, serviceName: "service"), start);
            parentSpan.SetDuration(TimeSpan.FromMilliseconds(100));

            var serverChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            serverChild.SetTag(Tags.SpanKind, SpanKinds.Server);
            serverChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(serverChild).Should().BeTrue();

            var clientChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            clientChild.SetTag(Tags.SpanKind, SpanKinds.Client);
            clientChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(clientChild).Should().BeTrue();

            var producerChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            producerChild.SetTag(Tags.SpanKind, SpanKinds.Producer);
            producerChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(producerChild).Should().BeTrue();

            var consumerChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            consumerChild.SetTag(Tags.SpanKind, SpanKinds.Consumer);
            consumerChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(consumerChild).Should().BeTrue();

            // Internal span kind should NOT be eligible (not top-level, not measured)
            var internalChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            internalChild.SetTag(Tags.SpanKind, SpanKinds.Internal);
            internalChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(internalChild).Should().BeFalse();

            // No span kind, not top-level, not measured => not eligible
            var plainChild = new Span(new SpanContext(parentSpan.Context, new TraceContext(new StubDatadogTracer()), "service"), start);
            plainChild.SetDuration(TimeSpan.FromMilliseconds(100));
            StatsAggregator.IsEligibleForStats(plainChild).Should().BeFalse();
        }

        [Fact]
        public async Task NewDimensionsCreateDistinctBuckets()
        {
            var start = DateTimeOffset.UtcNow;
            var aggregator = new StatsAggregator(Mock.Of<IApi>(), GetSettings(), Mock.Of<IDiscoveryService>());

            try
            {
                ulong id = 0;

                // Baseline
                var baselineSpan = CreateTopLevelSpan(++id, start, 100);

                // Unique SpanKind
                var spanKindSpan = CreateTopLevelSpan(++id, start, 100);
                spanKindSpan.SetTag(Tags.SpanKind, SpanKinds.Server);

                // Unique GrpcStatusCode
                var grpcSpan = CreateTopLevelSpan(++id, start, 100);
                grpcSpan.SetTag("rpc.grpc.status_code", "0");

                // Unique HttpMethod
                var httpMethodSpan = CreateTopLevelSpan(++id, start, 100);
                httpMethodSpan.SetTag(Tags.HttpMethod, "POST");

                // Unique HttpEndpoint
                var httpEndpointSpan = CreateTopLevelSpan(++id, start, 100);
                httpEndpointSpan.SetTag(Tags.HttpEndpoint, "/api/test");

                aggregator.Add(baselineSpan, spanKindSpan, grpcSpan, httpMethodSpan, httpEndpointSpan);

                var buffer = aggregator.CurrentBuffer;
                buffer.Buckets.Should().HaveCount(5, "each new dimension value should create a distinct bucket");
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

        private static Span CreateTopLevelSpan(ulong id, DateTimeOffset start, long durationMs, string serviceName = "service", string operationName = "op")
        {
            var span = new Span(new SpanContext(id, id, serviceName: serviceName), start);
            span.OperationName = operationName;
            span.SetDuration(TimeSpan.FromMilliseconds(durationMs));
            return span;
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

        private class StubDiscoveryService : IDiscoveryService
        {
            public void SubscribeToChanges(Action<AgentConfiguration> callback)
            {
                callback(new AgentConfiguration(
                             configurationEndpoint: "configurationEndpoint",
                             debuggerEndpoint: "debuggerEndpoint",
                             debuggerV2Endpoint: "debuggerV2Endpoint",
                             diagnosticsEndpoint: "diagnosticsEndpoint",
                             symbolDbEndpoint: "symbolDbEndpoint",
                             agentVersion: "agentVersion",
                             statsEndpoint: "traceStatsEndpoint",
                             dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                             eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                             telemetryProxyEndpoint: "telemetryProxyEndpoint",
                             tracerFlareEndpoint: "tracerFlareEndpoint",
                             clientDropP0: true,
                             spanMetaStructs: true,
                             spanEvents: true));
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
