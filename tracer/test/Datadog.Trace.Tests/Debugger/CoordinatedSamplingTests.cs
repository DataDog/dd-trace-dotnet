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
    public async Task SnapshotProbesAcrossSpansShareFirstDecisionAndCapEachProbe(bool firstDecision)
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
        var third = CreateProcessor(CreateLogProbe("probe-3", captureSnapshot: true), thirdGlobalLimiter);

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

    [Theory]
    [InlineData(false, false, false, true, 0)]
    [InlineData(true, false, true, true, 2)]
    [InlineData(false, true, true, true, 2)]
    [InlineData(true, true, true, false, 0)]
    public async Task ConditionalSnapshotsCoordinateAfterFirstTrueCondition(bool firstCondition, bool secondCondition, bool thirdCondition, bool firstDecision, int expectedSnapshots)
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
        Assert.Equal(expectedSnapshots == 0 && !firstCondition && !secondCondition && !thirdCondition ? 0 : 1, sampler.SampleCalls);
        Assert.Equal(sampler.SampleCalls, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task CaptureExpressionProbesParticipateInCoordinatedSampling()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        var firstSampler = new CountingSampler(true);
        var secondSampler = new CountingSampler(false);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var first = CreateProcessor(CreateCaptureExpressionProbe("probe-1"), globalLimiter);
        var second = CreateProcessor(CreateCaptureExpressionProbe("probe-2"), globalLimiter);

        Assert.True(TryBeginAndDispose(first, firstSampler));
        Assert.True(TryBeginAndDispose(second, secondSampler));
        Assert.Equal(1, firstSampler.SampleCalls);
        Assert.Equal(0, secondSampler.SampleCalls);
        Assert.Equal(1, globalLimiter.ShouldSampleCallCount);
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
    public async Task SnapshotProbesWithoutAnActiveTraceSampleIndependently()
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
    }

    [Fact]
    public async Task CoordinatedDecisionIsScopedToTrace()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);

        var sampler = new CountingSampler(true);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var processor = CreateProcessor(CreateLogProbe("probe-1", captureSnapshot: true), globalLimiter);

        using (tracer.StartActive("first-root"))
        {
            Assert.True(TryBeginAndDispose(processor, sampler));
            Assert.False(TryBeginAndDispose(processor, sampler));
        }

        using (tracer.StartActive("second-root"))
        {
            Assert.True(TryBeginAndDispose(processor, sampler));
        }

        Assert.Equal(2, sampler.SampleCalls);
        Assert.Equal(2, globalLimiter.ShouldSampleCallCount);
    }

    [Theory]
    [InlineData(false, true, 32)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 0)]
    public async Task ConcurrentFirstHitsConsultSamplerOnce(bool sameProbeId, bool samplerResult, int expectedEmissions)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);
        using var scope = (Scope)tracer.StartActive("root");

        const int probeCount = 32;
        var sampler = new CountingSampler(samplerResult, delayMilliseconds: 50);
        var globalLimiter = new GlobalRateLimiterMock(true);
        var gate = new ManualResetEventSlim();
        var tasks = new Task<bool>[probeCount];

        for (var i = 0; i < probeCount; i++)
        {
            var probeId = sameProbeId ? "probe" : $"probe-{i}";
            var processor = CreateProcessor(CreateLogProbe(probeId, captureSnapshot: true), globalLimiter);
            tasks[i] = Task.Run(
                () =>
                {
                    gate.Wait();
                    return TryBeginAndDispose(processor, sampler);
                });
        }

        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(expectedEmissions, results.Count(static result => result));
        Assert.Equal(1, sampler.SampleCalls);
        Assert.Equal(1, globalLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public async Task SnapshotUsesSpanThatWasActiveAtBegin()
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
    }

    [Fact]
    public async Task ExceptionReplaySnapshotKeepsSpanContextFromConstruction()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        Tracer.UnsafeSetTracerInstance(tracer);

        ExceptionReplaySnapshotCreator snapshotCreator;
        string expectedTraceId;
        string expectedSpanId;
        using (var scope = (Scope)tracer.StartActive("root"))
        {
            snapshotCreator = new ExceptionReplaySnapshotCreator(
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

        var snapshot = FinalizeSnapshot(snapshotCreator);

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
                Expr = new SnapshotSegment(string.Empty, @"{ ""ref"": ""value"" }", null)
            }
        ];
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

    private sealed class CountingSampler(bool result, int delayMilliseconds = 0) : IAdaptiveSampler
    {
        private int _sampleCalls;

        public int SampleCalls => Volatile.Read(ref _sampleCalls);

        public bool Sample()
        {
            Interlocked.Increment(ref _sampleCalls);
            if (delayMilliseconds > 0)
            {
                Thread.Sleep(delayMilliseconds);
            }

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
