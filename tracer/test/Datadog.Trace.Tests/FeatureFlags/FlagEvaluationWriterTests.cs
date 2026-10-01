// <copyright file="FlagEvaluationWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitFlushSendsPrivacySafeCompressedAggregates(bool consent)
    {
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context);
        try
        {
            writer.TryEnqueue(Observation(consent: consent)).Should().BeTrue();
            writer.TryEnqueue(Observation(consent: consent)).Should().BeTrue();
            await Completes(writer.FlushAsync());
            bodies.Should().ContainSingle();
            bodies.TryPeek(out var bytes).Should().BeTrue();
            var json = Encoding.UTF8.GetString(bytes!);
            json.Should().NotContain("private-error-canary");
            if (consent)
            {
                json.Should().Contain("subject-canary").And.Contain("context-canary");
            }
            else
            {
                json.Should().NotContain("subject-canary").And.NotContain("context-canary").And.Contain("sha256_");
            }

            var payload = JObject.Parse(json);
            payload["flagEvaluations"]![0]!["evaluation_count"]!.Value<int>().Should().Be(2);
            payload["context"]!["service"]!.Value<string>().Should().Be("writer-test");
            await Completes(writer.FlushAsync());
            bodies.Should().ContainSingle("a drained batch must not be sent again");
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task SparseEvaluationFlushesWithoutExplicitRequest()
    {
        var sent = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                sent.TrySetResult(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context,
            flushInterval: TimeSpan.FromMilliseconds(20));
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(sent.Task);
            JObject.Parse(Encoding.UTF8.GetString(await sent.Task))["flagEvaluations"]![0]!["evaluation_count"]!.Value<int>().Should().Be(1);
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task BlockedSenderDoesNotBlockAdmissionOrGrowQueueBeyondCapacity()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                entered.TrySetResult(true);
                return release.Task;
            },
            Context,
            queueCap: 2);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var flush = writer.FlushAsync();
            await Completes(entered.Task);
            writer.TryEnqueue(Observation()).Should().BeTrue();
            writer.TryEnqueue(Observation()).Should().BeTrue();
            writer.HasCapacity().Should().BeFalse();
            var offers = Task.Run(() =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    writer.TryEnqueue(Observation()).Should().BeFalse();
                }
            });
            await Completes(offers);
            flush.IsCompleted.Should().BeFalse();
            release.TrySetResult(true);
            await Completes(flush);
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task CloseReturnsAtDeadlineAndStopsAdmissionDuringBlockedSend()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult(true);
                return release.Task;
            },
            Context);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var flush = writer.FlushAsync();
            await Completes(entered.Task);
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(writer.CloseAsync(TimeSpan.FromMilliseconds(50)));
            release.Task.IsCompleted.Should().BeFalse();
            writer.HasCapacity().Should().BeFalse();
            writer.TryEnqueue(Observation()).Should().BeFalse();
            release.TrySetResult(true);
            await Completes(flush);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            Volatile.Read(ref calls).Should().Be(1, "timed-out close must not start another network batch");
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task FailedBatchDoesNotPoisonFollowingFlush()
    {
        var calls = 0;
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new IOException("private-send-error-canary");
                }

                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(writer.FlushAsync());
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(writer.FlushAsync());
            Volatile.Read(ref calls).Should().Be(2);
            bodies.Should().ContainSingle();
            bodies.TryPeek(out var bytes).Should().BeTrue();
            JObject.Parse(Encoding.UTF8.GetString(bytes!))["flagEvaluations"]![0]!["evaluation_count"]!.Value<int>().Should().Be(1);
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task EmptyWriterDoesNotSendOnFlushOrClose()
    {
        var calls = 0;
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            },
            Context);
        await Completes(writer.FlushAsync());
        await writer.CloseAsync(TimeSpan.FromSeconds(2));
        Volatile.Read(ref calls).Should().Be(0);
    }

    [Fact]
    public async Task QueueDropsAndSnapshotOmissionsAreCountedOnceAtAdmission()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                entered.TrySetResult(true);
                return release.Task;
            },
            Context,
            queueCap: 1,
            metrics: collector);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var flush = writer.FlushAsync();
            await Completes(entered.Task);
            var oversized = new FlagEvalEvent("flag", "variant", null, "user", 1790000000000, new Dictionary<string, object?> { ["field"] = new string('x', 257) }, observeFullEvaluationData: true);
            writer.TryEnqueue(oversized, (int)ContextOmissionReason.MaxValueLength).Should().BeTrue();
            writer.HasCapacity().Should().BeFalse();
            writer.TryEnqueue(Observation(), (int)ContextOmissionReason.SnapshotError).Should().BeFalse();
            release.TrySetResult(true);
            await Completes(flush);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            writer.TryEnqueue(Observation()).Should().BeFalse();

            ReadMetrics(collector).Should().BeEquivalentTo(new Dictionary<string, double>
            {
                ["flagevaluation.rows.dropped|reason:pre_queue_overflow"] = 1,
                ["flagevaluation.rows.dropped|reason:queue_overflow"] = 1,
                ["flagevaluation.rows.dropped|reason:closed"] = 1,
                ["flagevaluation.context.truncated|reason:max_value_length"] = 1,
                ["flagevaluation.context.truncated|reason:snapshot_error"] = 1,
            });
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public async Task AggregationMetricsCountEvaluationsRatherThanBuckets()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context,
            globalCap: 0,
            degradedCap: 1,
            metrics: collector);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            writer.TryEnqueue(Observation()).Should().BeTrue();
            writer.TryEnqueue(Observation(targetingKey: "\uD800")).Should().BeTrue();
            for (var i = 0; i < 3; i++)
            {
                writer.TryEnqueue(Observation(flag: "another-flag")).Should().BeTrue();
            }

            await Completes(writer.FlushAsync());
            ReadMetrics(collector).Should().BeEquivalentTo(new Dictionary<string, double>
            {
                ["flagevaluation.rows.degraded|reason:cardinality_cap"] = 3,
                ["flagevaluation.rows.dropped|reason:degraded_cap"] = 3,
                ["flagevaluation.targeting_key.omitted|reason:invalid"] = 1,
            });
            bodies.Should().ContainSingle();
            bodies.TryPeek(out var bytes).Should().BeTrue();
            var row = JObject.Parse(Encoding.UTF8.GetString(bytes!))["flagEvaluations"]![0]!;
            row["evaluation_count"]!.Value<int>().Should().Be(3);
            row["targeting_key"].Should().BeNull();
            row["context"].Should().BeNull();
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public async Task ThrowingMetricSinkCannotLoseAnOtherwiseValidEvent()
    {
        var brokenCollector = new Mock<IMetricsTelemetryCollector>(MockBehavior.Strict);
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context,
            metrics: brokenCollector.Object);
        try
        {
            writer.TryEnqueue(Observation(targetingKey: "\uD800"), (int)ContextOmissionReason.SnapshotError).Should().BeTrue();
            await Completes(writer.FlushAsync());
            bodies.Should().ContainSingle();
            bodies.TryPeek(out var bytes).Should().BeTrue();
            var row = JObject.Parse(Encoding.UTF8.GetString(bytes!))["flagEvaluations"]![0]!;
            row["evaluation_count"]!.Value<int>().Should().Be(1);
            row["targeting_key"].Should().BeNull();
            brokenCollector.Invocations.Should().HaveCount(2, "both admission and flush should attempt their metric despite the failing sink");
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task FlushDuringBlockedSendWaitsForTheNextBatchAndCoalescesCallers()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                entered.TrySetResult(true);
                return release.Task;
            },
            Context);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var first = writer.FlushAsync();
            await Completes(entered.Task);
            writer.TryEnqueue(Observation(flag: "next")).Should().BeTrue();
            var next = writer.FlushAsync();
            next.Should().NotBeSameAs(first);
            writer.FlushAsync().Should().BeSameAs(next);
            next.IsCompleted.Should().BeFalse();
            release.TrySetResult(true);
            await Completes(next);
            bodies.Should().HaveCount(2);
            JObject.Parse(Encoding.UTF8.GetString(bodies.Last()))["flagEvaluations"]![0]!["flag"]!["key"]!.Value<string>().Should().Be("next");
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task RepeatedIdleWakeCyclesPreserveEveryObservationAndFlush()
    {
        long delivered = 0;
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                var rows = JObject.Parse(Encoding.UTF8.GetString(Decompress(bytes)))["flagEvaluations"]!;
                Interlocked.Add(ref delivered, rows.Sum(row => row["evaluation_count"]!.Value<long>()));
                return Task.CompletedTask;
            },
            Context,
            queueCap: 1,
            flushInterval: TimeSpan.FromHours(1));
        try
        {
            for (var cycle = 0; cycle < 50; cycle++)
            {
                // Exercise both work arriving around a wait and a worker already asleep.
                if (cycle % 2 == 0)
                {
                    await Task.Delay(1);
                }

                writer.TryEnqueue(Observation()).Should().BeTrue();
                SpinWait.SpinUntil(writer.HasCapacity, TimeSpan.FromSeconds(5)).Should().BeTrue(
                    "enqueue must wake the idle worker without an explicit flush or timer");
                await Completes(writer.FlushAsync());
                Volatile.Read(ref delivered).Should().Be(cycle + 1);
                await Completes(writer.FlushAsync());
                Volatile.Read(ref delivered).Should().Be(cycle + 1, "empty flushes must not resend observations");
            }
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task EmptyFlushRequestedDuringSendCompletesWithoutWaitingForTimer()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult(true);
                return release.Task;
            },
            Context,
            flushInterval: TimeSpan.FromHours(1));
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var first = writer.FlushAsync();
            await Completes(entered.Task);
            var next = writer.FlushAsync();
            next.Should().NotBeSameAs(first);
            next.IsCompleted.Should().BeFalse();
            release.TrySetResult(true);
            await Completes(next);
            await Completes(first);
            Volatile.Read(ref calls).Should().Be(1);
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ContinuousProducersDoNotStarvePeriodicFlushOrClose()
    {
        var sent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        long accepted = 0;
        long delivered = 0;
        using var stop = new CancellationTokenSource();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                var rows = JObject.Parse(Encoding.UTF8.GetString(Decompress(bytes)))["flagEvaluations"]!;
                foreach (var row in rows)
                {
                    Interlocked.Add(ref delivered, row["evaluation_count"]!.Value<long>());
                }

                sent.TrySetResult(true);
                return Task.CompletedTask;
            },
            Context,
            queueCap: 64,
            flushInterval: TimeSpan.FromMilliseconds(20));
        var observation = Observation();
        var producers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                if (writer.TryEnqueue(observation))
                {
                    Interlocked.Increment(ref accepted);
                }
            }
        })).ToArray();
        try
        {
            await Completes(sent.Task);
            stop.IsCancellationRequested.Should().BeFalse();
            await Completes(writer.CloseAsync(TimeSpan.FromSeconds(2)));
            stop.Cancel();
            await Completes(Task.WhenAll(producers));
            delivered.Should().Be(accepted);
            accepted.Should().BeGreaterThan(0);
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAll(producers);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task AbandonedQueueIsReleasedByItsConsumerAndCountedAfterBlockedSendEnds()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FlagEvaluationWriter(
            _ =>
            {
                entered.TrySetResult(true);
                return release.Task;
            },
            Context,
            metrics: collector);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            var flush = writer.FlushAsync();
            await Completes(entered.Task);
            var reference = EnqueueTrackedObservation(writer);
            await writer.CloseAsync(TimeSpan.FromMilliseconds(20));
            reference.IsAlive.Should().BeTrue("the blocked consumer, not shutdown, owns queue cleanup");
            release.TrySetResult(true);
            await Completes(flush);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            reference.IsAlive.Should().BeFalse("the finished consumer must release abandoned observations");
            ReadMetrics(collector).Should().BeEquivalentTo(new Dictionary<string, double>
            {
                ["flagevaluation.rows.dropped|reason:closed"] = 1,
            });
        }
        finally
        {
            release.TrySetResult(true);
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public async Task ContextFailureDropsTheBatchButDoesNotPoisonFollowingFlush()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var calls = 0;
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            () => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("private-context-error") : Context(),
            metrics: collector);
        try
        {
            writer.TryEnqueue(Observation()).Should().BeTrue();
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(writer.FlushAsync());
            bodies.Should().BeEmpty();
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await Completes(writer.FlushAsync());
            bodies.Should().ContainSingle();
            ReadMetrics(collector).Should().BeEquivalentTo(new Dictionary<string, double>
            {
                ["flagevaluation.rows.dropped|reason:serialization_error"] = 2,
            });
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadLimitMetricsPreserveEvaluationUnitsAndGoodNeighbors(bool irreducible)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<byte[]>();
        var writer = new FlagEvaluationWriter(
            bytes =>
            {
                bodies.Enqueue(Decompress(bytes));
                return Task.CompletedTask;
            },
            Context,
            payloadLimitBytes: 512,
            metrics: collector);
        try
        {
            writer.TryEnqueue(new FlagEvalEvent("before", "on", null, null, 1790000000000, null)).Should().BeTrue();
            for (var i = 0; i < 3; i++)
            {
                writer.TryEnqueue(new FlagEvalEvent(irreducible ? new string('f', 5000) : "large", "on", null, new string('t', 5000), 1790000000000, null, observeFullEvaluationData: true)).Should().BeTrue();
            }

            writer.TryEnqueue(new FlagEvalEvent("after", "on", null, null, 1790000000000, null)).Should().BeTrue();
            await Completes(writer.FlushAsync());
            bodies.Should().OnlyContain(body => body.Length <= 512);
            var rows = bodies.SelectMany(body => JObject.Parse(Encoding.UTF8.GetString(body))["flagEvaluations"]!).ToList();
            rows.Sum(row => row["evaluation_count"]!.Value<long>()).Should().Be(irreducible ? 2 : 5);
            rows.Select(row => row["flag"]!["key"]!.Value<string>()).Should().Contain("before").And.Contain("after");
            var expected = new Dictionary<string, double>
            {
                [irreducible ? "flagevaluation.rows.dropped|reason:payload_limit" : "flagevaluation.rows.degraded|reason:payload_limit"] = 3,
            };
            if (bodies.Count > 1)
            {
                expected["flagevaluation.payload.splits|"] = bodies.Count - 1;
            }

            ReadMetrics(collector).Should().BeEquivalentTo(expected);
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueTrackedObservation(FlagEvaluationWriter writer)
    {
        var observation = Observation(consent: true);
        writer.TryEnqueue(observation).Should().BeTrue();
        return new WeakReference(observation);
    }

    private static Dictionary<string, double> ReadMetrics(MetricsTelemetryCollector collector)
    {
        collector.AggregateMetrics();
        var metrics = collector.GetMetrics().Metrics!;
        metrics.Should().NotBeNull();
        // The tracer's default namespace is represented by an omitted namespace field.
        metrics.Should().OnlyContain(metric => (metric.Namespace ?? "tracers") == "tracers" && metric.Common);
        return metrics.ToDictionary(
            metric => metric.Metric + "|" + string.Join(",", metric.Tags ?? []),
            metric => metric.Points.Sum(point => (double)point.Value));
    }

    private static IReadOnlyDictionary<string, string> Context() => new Dictionary<string, string>
    {
        ["service"] = "writer-test",
        ["env"] = "test",
        ["version"] = "1",
    };

    private static FlagEvalEvent Observation(bool consent = false, string flag = "flag", string? targetingKey = "subject-canary") => new(
        flag,
        "variant",
        "allocation",
        targetingKey,
        1790000000000,
        new Dictionary<string, object?> { ["country"] = "context-canary" },
        "private-error-canary",
        consent);

    private static async Task Completes(Task task)
    {
        (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(task);
        await task;
    }

    private static byte[] Decompress(ArraySegment<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
