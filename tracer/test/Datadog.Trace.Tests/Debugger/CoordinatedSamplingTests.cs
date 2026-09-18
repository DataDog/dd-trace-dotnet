// <copyright file="CoordinatedSamplingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class CoordinatedSamplingTests
{
    private const int TestMaxEvaluationTimeInMilliseconds = 30_000;
    private const string TrueConditionJson = @"{ ""eq"": [1, 1] }";
    private const string FalseConditionJson = @"{ ""eq"": [1, 0] }";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SnapshotAndCaptureExpressionProbesShareFirstDecisionAndCapEachProbe(bool firstDecision)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        var firstSampler = new CountingSampler(firstDecision);
        var secondSampler = new CountingSampler(!firstDecision);
        var thirdSampler = new CountingSampler(!firstDecision);
        var firstGlobalLimiter = new GlobalRateLimiterMock(true);
        var secondGlobalLimiter = new GlobalRateLimiterMock(!firstDecision);
        var thirdGlobalLimiter = new GlobalRateLimiterMock(!firstDecision);
        var first = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: true), firstGlobalLimiter);
        var second = CreateProcessor(CreateLogProbe("probe-2", captureSnapshot: true), secondGlobalLimiter);
        var third = CreateProcessor(CreateCaptureExpressionProbe("probe-3"), thirdGlobalLimiter);

        Assert.Equal(firstDecision, TryBeginAndDispose(first, firstSampler));
        using (tracer.StartActive("child"))
        {
            Assert.Equal(firstDecision, TryBeginAndDispose(second, secondSampler));
        }

        using (tracer.StartActive("sibling"))
        {
            Assert.Equal(firstDecision, TryBeginAndDispose(third, thirdSampler));
        }

        Assert.False(TryBeginAndDispose(first, firstSampler));

        Assert.Equal(1, firstSampler.SampleCalls);
        Assert.Equal(0, secondSampler.SampleCalls);
        Assert.Equal(0, thirdSampler.SampleCalls);
        Assert.Equal(1, firstGlobalLimiter.ShouldSampleCallCount);
        Assert.Equal(0, secondGlobalLimiter.ShouldSampleCallCount);
        Assert.Equal(0, thirdGlobalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task CaptureExpressionWithoutValuesReleasesProbeSlot()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        var sampler = new CountingSampler(true);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var processor = CreateProcessor(CreateUndefinedCaptureExpressionProbe("probe"), globalLimiter);

        Assert.False(EvaluateConditionalAtEntry(processor, sampler));

        processor.UpdateProbeProcessor(CreateCaptureExpressionProbe("probe"), TestMaxEvaluationTimeInMilliseconds);

        Assert.True(EvaluateConditionalAtEntry(processor, sampler));
        Assert.Equal(1, sampler.SampleCalls);
        Assert.Equal(1, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task CoordinatedDropRecordsEverySkippedSnapshot()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var metricsScope = DebuggerGuardrailMetricTestHelpers.OverrideMetrics(out var collector);
        using var scope = (Scope)tracer.StartActive("root");

        var firstSampler = new CountingSampler(true);
        var secondSampler = new CountingSampler(true);
        var firstGlobalLimiter = new GlobalRateLimiterMock(false);
        var secondGlobalLimiter = new GlobalRateLimiterMock(true);
        var first = CreateProcessor(CreateLogProbe("snapshot-probe-1", captureSnapshot: true), firstGlobalLimiter);
        var second = CreateProcessor(CreateLogProbe("snapshot-probe-2", captureSnapshot: true), secondGlobalLimiter);

        Assert.False(TryBeginAndDispose(first, firstSampler));
        Assert.False(TryBeginAndDispose(second, secondSampler));

        Assert.Equal(0, firstSampler.SampleCalls);
        Assert.Equal(0, secondSampler.SampleCalls);
        Assert.Equal(1, firstGlobalLimiter.ShouldSampleCallCount);
        Assert.Equal(0, secondGlobalLimiter.ShouldSampleCallCount);
        collector.AssertHasCount("events.skipped", "reason:rateLimitGlobal", "event_type:snapshot", expected: 2);
    }

    [Theory]
    [InlineData(false, false, false, true, 0, 0)]
    [InlineData(false, true, true, true, 2, 1)]
    [InlineData(true, true, true, false, 0, 1)]
    public async Task ConditionalSnapshotsCoordinateAfterFirstTrueCondition(bool firstCondition, bool secondCondition, bool thirdCondition, bool firstDecision, int expectedSnapshots, int expectedSampleCalls)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        var sampler = new CountingSampler(firstDecision);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var conditions = new[] { firstCondition, secondCondition, thirdCondition };
        var snapshots = 0;

        for (var i = 0; i < conditions.Length; i++)
        {
            var probeId = $"probe-{i}";
            var probe = CreateConditionalLogProbe(probeId, conditions[i] ? TrueConditionJson : FalseConditionJson);
            var processor = CreateProcessor(probe, globalLimiter);
            if (EvaluateConditionalAtEntry(processor, sampler))
            {
                snapshots++;
            }
        }

        Assert.Equal(expectedSnapshots, snapshots);
        Assert.Equal(expectedSampleCalls, sampler.SampleCalls);
        Assert.Equal(expectedSampleCalls, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task TemplateLogsSampleIndependently()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        var firstSampler = new CountingSampler(true);
        var secondSampler = new CountingSampler(false);
        var globalLimiter = new GlobalRateLimiterMock(false);
        var first = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: false), globalLimiter);
        var second = CreateProcessor(CreateLogProbe("probe-2", captureSnapshot: false), globalLimiter);

        Assert.True(TryBeginAndDispose(first, firstSampler));
        Assert.False(TryBeginAndDispose(second, secondSampler));
        Assert.Equal(1, firstSampler.SampleCalls);
        Assert.Equal(1, secondSampler.SampleCalls);
        Assert.Equal(0, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task CoordinatedDecisionIsLocalToEachTrace()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);

        var sampler = new CountingSampler(true);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var processor = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: true), globalLimiter);

        Assert.True(TryBeginAndDispose(processor, sampler));
        Assert.True(TryBeginAndDispose(processor, sampler));
        Assert.Equal(2, sampler.SampleCalls);
        Assert.Equal(2, globalLimiter.ShouldSampleCallCount);

        using (tracer.StartActive("first-root"))
        {
            Assert.True(TryBeginAndDispose(processor, sampler));
            Assert.False(TryBeginAndDispose(processor, sampler));
        }

        using (tracer.StartActive("second-root"))
        {
            Assert.True(TryBeginAndDispose(processor, sampler));
        }

        Assert.Equal(4, sampler.SampleCalls);
        Assert.Equal(4, globalLimiter.ShouldSampleCallCount);
    }

    [Theory]
    [InlineData(false, true, 8)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 0)]
    public async Task ConcurrentFirstHitsShareTraceDecisionAndCapEachProbe(bool sameProbeId, bool samplerResult, int expectedEmissions)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        const int probeCount = 8;
        var enteredSample = new ManualResetEventSlim();
        var releaseSample = new ManualResetEventSlim();
        var sampler = new BlockingSampler(samplerResult, enteredSample, releaseSample);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var start = new Barrier(probeCount + 1);
        var results = new bool[probeCount];
        var threads = new Thread[probeCount];

        for (var i = 0; i < probeCount; i++)
        {
            var index = i;
            var probeId = sameProbeId ? "probe" : $"probe-{index}";
            var processor = CreateProcessor(CreateLogProbe(probeId, captureSnapshot: true), globalLimiter);
            threads[i] = new Thread(
                () =>
                {
                    start.SignalAndWait();
                    results[index] = TryBeginAndDispose(processor, sampler);
                })
            {
                IsBackground = true
            };
            threads[i].Start();
        }

        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10)));
        Assert.True(enteredSample.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(
            SpinWait.SpinUntil(
                () => threads.All(static thread => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0),
                TimeSpan.FromSeconds(10)),
            "Expected every first hit to wait for the in-flight Sample()");

        releaseSample.Set();
        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        }

        Assert.Equal(expectedEmissions, results.Count(static result => result));
        Assert.Equal(1, sampler.SampleCalls);
        Assert.Equal(1, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public void CreatingDecisionPreventsReentrantSamplerConsult()
    {
        DebuggerSamplingCoordinator.State state = null;
        var calls = 0;

        bool Sample()
        {
            Interlocked.Increment(ref calls);
            Assert.False(DebuggerSamplingCoordinator.TrySample(ref state, "nested", new DelegateProvider(Sample)));
            return true;
        }

        Assert.True(DebuggerSamplingCoordinator.TrySample(ref state, "outer", new DelegateProvider(Sample)));
        Assert.Equal(1, calls);
        Assert.True(DebuggerSamplingCoordinator.TrySample(ref state, "nested", new DelegateProvider(Sample)));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ThrownSampleClearsCreatingDecisionSoALaterCallerCanRetry()
    {
        DebuggerSamplingCoordinator.State state = null;
        var calls = 0;

        bool Sample()
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                throw new InvalidOperationException("first-hit sample failed");
            }

            return true;
        }

        var provider = new DelegateProvider(Sample);
        Assert.Throws<InvalidOperationException>(() => DebuggerSamplingCoordinator.TrySample(ref state, "first", provider));
        Assert.Equal(1, calls);
        Assert.True(DebuggerSamplingCoordinator.TrySample(ref state, "retry", provider));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void WaitingCallerRetriesAfterFirstSamplerThrows()
    {
        DebuggerSamplingCoordinator.State state = null;
        var enteredFirstSample = new ManualResetEventSlim();
        var releaseFirstSample = new ManualResetEventSlim();
        Exception firstException = null;
        var secondSampleCalls = 0;
        var secondResult = false;

        var first = new Thread(
            () =>
            {
                try
                {
                    DebuggerSamplingCoordinator.TrySample(
                        ref state,
                        "first",
                        new DelegateProvider(
                            () =>
                            {
                                enteredFirstSample.Set();
                                releaseFirstSample.Wait();
                                throw new InvalidOperationException("first-hit sample failed");
                            }));
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            })
        {
            IsBackground = true
        };
        var second = new Thread(
            () =>
            {
                secondResult = DebuggerSamplingCoordinator.TrySample(
                    ref state,
                    "second",
                    new DelegateProvider(
                        () =>
                        {
                            Interlocked.Increment(ref secondSampleCalls);
                            return true;
                        }));
            })
        {
            IsBackground = true
        };

        first.Start();
        try
        {
            Assert.True(enteredFirstSample.Wait(TimeSpan.FromSeconds(10)));
            second.Start();
            Assert.True(
                SpinWait.SpinUntil(
                    () => (second.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(10)),
                "Expected the second caller to wait for the in-flight Sample()");
        }
        finally
        {
            releaseFirstSample.Set();
        }

        Assert.True(first.Join(TimeSpan.FromSeconds(10)));
        Assert.True(second.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(firstException);
        Assert.True(secondResult);
        Assert.Equal(1, secondSampleCalls);
        var unexpectedProvider = new DelegateProvider(() => throw new InvalidOperationException());
        Assert.False(DebuggerSamplingCoordinator.TrySample(ref state, "second", unexpectedProvider));
        Assert.True(DebuggerSamplingCoordinator.TrySample(ref state, "first", unexpectedProvider));
    }

    [Fact]
    public async Task FirstHitSamplingDoesNotBlockSpanClose()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using (tracer.StartActive("root"))
        {
            var child = (Scope)tracer.StartActive("child");
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var sampler = new BlockingSampler(result: true, entered, release);
            var processor = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: true), new GlobalRateLimiterMock(true));
            var emitted = false;
            var sampling = new Thread(() => emitted = TryBeginAndDispose(processor, sampler))
            {
                IsBackground = true
            };

            sampling.Start();
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                var closing = Task.Run(() => child.Span.Finish());
                var completed = await Task.WhenAny(closing, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.True(completed == closing, "CloseSpan waited on debugger first-hit sampling");
                await closing;
            }
            finally
            {
                release.Set();
            }

            Assert.True(sampling.Join(TimeSpan.FromSeconds(10)));
            Assert.True(emitted);
            Assert.Equal(1, sampler.SampleCalls);
        }
    }

    [Fact]
    public async Task SnapshotsUseSpanContextCapturedAtBegin()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);

        DebuggerSnapshotCreator snapshotCreator;
        string expectedTraceId;
        string expectedSpanId;
        using (tracer.StartActive("first-root"))
        {
            using (var childScope = (Scope)tracer.StartActive("child"))
            {
                var processor = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: true), new GlobalRateLimiterMock(true));
                var probeData = new ProbeData("probe-1", new CountingSampler(true), processor);
                Assert.True(processor.TryBeginProcess(in probeData, out var creator));
                snapshotCreator = (DebuggerSnapshotCreator)creator;
                expectedTraceId = childScope.Span.TraceId128.Lower.ToString();
                expectedSpanId = childScope.Span.SpanId.ToString();
            }
        }

        JObject snapshot;
        using (var secondRootScope = (Scope)tracer.StartActive("second-root"))
        {
            Assert.NotEqual(expectedTraceId, secondRootScope.Span.TraceId128.Lower.ToString());
            snapshot = FinalizeSnapshot(snapshotCreator);
        }

        Assert.Equal(expectedTraceId, snapshot["dd.trace_id"]?.Value<string>());
        Assert.Equal(expectedSpanId, snapshot["dd.span_id"]?.Value<string>());

        ExceptionReplaySnapshotCreator exceptionReplayCreator;
        using (var scope = (Scope)tracer.StartActive("exception-replay"))
        {
            exceptionReplayCreator = new ExceptionReplaySnapshotCreator(
                isFullSnapshot: true,
                ProbeLocation.Method,
                hasCondition: false,
                tags: [],
                CreateCaptureLimitInfo(),
                processTagsProvider: static () => null,
                serviceNameProvider: static () => "test-service");
            expectedTraceId = scope.Span.TraceId128.Lower.ToString();
            expectedSpanId = scope.Span.SpanId.ToString();
        }

        snapshot = FinalizeSnapshot(exceptionReplayCreator);
        Assert.Equal(expectedTraceId, snapshot["dd.trace_id"]?.Value<string>());
        Assert.Equal(expectedSpanId, snapshot["dd.span_id"]?.Value<string>());
    }

    private static ProbeProcessor CreateProcessor(LogProbe probe, IDebuggerGlobalRateLimiter globalRateLimiter)
        => new(probe, TestMaxEvaluationTimeInMilliseconds, globalRateLimiter);

    private static LogProbe CreateLogProbe(string probeId, bool captureSnapshot)
        => new()
        {
            Id = probeId,
            CaptureSnapshot = captureSnapshot,
            EvaluateAt = EvaluateAt.Entry,
            Where = new Where
            {
                TypeName = typeof(CoordinatedSamplingTests).FullName!,
                MethodName = nameof(DummyMethod)
            },
            Tags = [],
        };

    private static LogProbe CreateConditionalLogProbe(string probeId, string conditionJson)
    {
        var probe = CreateLogProbe(probeId, captureSnapshot: true);
        probe.When = new SnapshotSegment(dsl: string.Empty, json: conditionJson, str: null);
        return probe;
    }

    private static LogProbe CreateCaptureExpressionProbe(string probeId)
    {
        var probe = CreateLogProbe(probeId, captureSnapshot: false);
        probe.CaptureExpressions =
        [
            new CaptureExpression
            {
                Name = "value",
                Expr = new SnapshotSegment(string.Empty, @"{ ""ref"": ""argument"" }", null)
            }
        ];
        return probe;
    }

    private static LogProbe CreateUndefinedCaptureExpressionProbe(string probeId)
    {
        var probe = CreateLogProbe(probeId, captureSnapshot: false);
        probe.CaptureExpressions = [new CaptureExpression { Name = "missingValue" }];
        return probe;
    }

    private static bool TryBeginAndDispose(ProbeProcessor processor, IAdaptiveSampler sampler)
    {
        var probeData = new ProbeData("unused", sampler, processor);
        var result = processor.TryBeginProcess(in probeData, out var snapshotCreator);
        (snapshotCreator as IDisposable)?.Dispose();
        return result;
    }

    private static bool EvaluateConditionalAtEntry(ProbeProcessor processor, IAdaptiveSampler sampler)
    {
        var probeData = new ProbeData("unused", sampler, processor);
        Assert.True(processor.TryBeginProcess(in probeData, out var creator));
        using var snapshotCreator = (DebuggerSnapshotCreator)creator;
        var captureInfo = new CaptureInfo<object>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryAsync,
            value: new object(),
            method: typeof(CoordinatedSamplingTests).GetMethod(nameof(DummyMethod), BindingFlags.Static | BindingFlags.NonPublic)!,
            invocationTargetType: typeof(object),
            memberKind: ScopeMemberKind.Argument,
            type: typeof(object),
            name: "argument",
            localsCount: 0,
            argumentsCount: 0,
            asyncCaptureInfo: new AsyncCaptureInfo(
                moveNextInvocationTarget: new object(),
                kickoffInvocationTarget: new object(),
                kickoffInvocationTargetType: typeof(CoordinatedSamplingTests),
                hoistedArgs: [],
                hoistedLocals: []));

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static JObject FinalizeSnapshot(DebuggerSnapshotCreator snapshotCreator)
    {
        var method = typeof(CoordinatedSamplingTests).GetMethod(nameof(DummyMethod), BindingFlags.Static | BindingFlags.NonPublic)!;
        var captureInfo = new CaptureInfo<object>(
            methodMetadataIndex: 0,
            methodState: MethodState.ExitEnd,
            value: new object(),
            method: method,
            type: typeof(object),
            invocationTargetType: typeof(CoordinatedSamplingTests),
            memberKind: ScopeMemberKind.This);

        return JObject.Parse(snapshotCreator.FinalizeMethodSnapshot("probe-id", 1, ref captureInfo));
    }

    private static CaptureLimitInfo CreateCaptureLimitInfo()
        => new(
            MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
            MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
            MaxLength: DebuggerSettings.DefaultMaxStringLength,
            MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy);

    private static void DummyMethod()
    {
    }

    private readonly struct DelegateProvider(Func<bool> sample) : IDebuggerSamplingDecisionProvider
    {
        public DebuggerSamplingDecision Sample()
            => sample() ? DebuggerSamplingDecision.Keep : DebuggerSamplingDecision.DropProbe;
    }

    private sealed class BlockingSampler(bool result, ManualResetEventSlim entered, ManualResetEventSlim release) : IAdaptiveSampler
    {
        private int _sampleCalls;

        public int SampleCalls => Volatile.Read(ref _sampleCalls);

        public bool Sample()
        {
            Interlocked.Increment(ref _sampleCalls);
            entered.Set();
            release.Wait();
            return result;
        }

        public bool Keep() => Sample();

        public bool Drop() => !Sample();

        public double NextDouble() => 0;

        public void Dispose()
        {
        }
    }

    private sealed class CountingSampler(bool result) : IAdaptiveSampler
    {
        private int _sampleCalls;

        public int SampleCalls => Volatile.Read(ref _sampleCalls);

        public bool Sample()
        {
            Interlocked.Increment(ref _sampleCalls);
            return result;
        }

        public bool Keep() => Sample();

        public bool Drop() => !Sample();

        public double NextDouble() => 0;

        public void Dispose()
        {
        }
    }

    private sealed class GlobalRateLimiterMock(bool result) : IDebuggerGlobalRateLimiter
    {
        private int _shouldSampleCallCount;

        public int ShouldSampleCallCount => Volatile.Read(ref _shouldSampleCallCount);

        public bool ShouldSampleSnapshot(string probeId)
        {
            Interlocked.Increment(ref _shouldSampleCallCount);
            return result;
        }

        public void Initialize()
        {
        }

        public void SetRate(double? samplesPerSecond)
        {
        }

        public void ResetRate()
        {
        }

        public void Dispose()
        {
        }
    }
}
