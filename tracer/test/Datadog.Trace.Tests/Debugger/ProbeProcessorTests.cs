// <copyright file="ProbeProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeProcessorTests
{
    private const string FalseConditionJson = @"{ ""eq"": [1, 0] }";
    private const string TrueConditionJson = @"{ ""eq"": [1, 1] }";

    [Fact]
    public void ShouldProcess_UnconditionalLogProbe_SamplesGlobalBeforePerProbe()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new AdaptiveSamplerMock(true);
        var probe = CreateLogProbe("log-probe", captureSnapshot: false);
        var processor = new ProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.ShouldProcess(in probeData);

        Assert.False(shouldProcess);
        Assert.Equal(1, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(ProbeType.Log, globalRateLimiter.LastProbeType);
        Assert.Equal("log-probe", globalRateLimiter.LastProbeId);
        Assert.Equal(0, perProbeSampler.SampleCallCount);
    }

    [Fact]
    public void Process_ConditionalProbe_EvaluatesConditionBeforeRateLimiting()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new AdaptiveSamplerMock(true);
        var probe = CreateConditionalLogProbe("conditional-false", FalseConditionJson, captureSnapshot: true);
        var processor = new ProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);
        var snapshotCreator = processor.CreateSnapshotCreator();
        var captureInfo = CreateAsyncEvaluateCaptureInfo();

        var result = processor.Process(ref captureInfo, snapshotCreator, in probeData);

        Assert.False(result);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(0, perProbeSampler.SampleCallCount);
    }

    [Fact]
    public void Process_ConditionalProbe_SamplesGlobalBeforePerProbe()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new AdaptiveSamplerMock(true);
        var probe = CreateConditionalLogProbe("conditional-true", TrueConditionJson, captureSnapshot: true);
        var processor = new ProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);
        var snapshotCreator = processor.CreateSnapshotCreator();
        var captureInfo = CreateAsyncEvaluateCaptureInfo();

        var result = processor.Process(ref captureInfo, snapshotCreator, in probeData);

        Assert.False(result);
        Assert.Equal(1, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(ProbeType.Snapshot, globalRateLimiter.LastProbeType);
        Assert.Equal("conditional-true", globalRateLimiter.LastProbeId);
        Assert.Equal(0, perProbeSampler.SampleCallCount);
    }

    [Fact]
    public void ShouldProcess_MetricProbe_DoesNotUseGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new AdaptiveSamplerMock(true);
        var probe = new MetricProbe
        {
            Id = "metric-probe",
            MetricName = "metric",
            Kind = MetricKind.COUNT,
            Where = new Where { MethodName = nameof(TestMethod) },
            Tags = [],
        };
        var processor = new ProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.ShouldProcess(in probeData);

        Assert.True(shouldProcess);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(1, perProbeSampler.SampleCallCount);
    }

    [Fact]
    public void ShouldProcess_SpanDecorationProbe_DoesNotUseGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new AdaptiveSamplerMock(true);
        var probe = new SpanDecorationProbe
        {
            Id = "span-probe",
            Decorations = [],
            TargetSpan = TargetSpan.Active,
            Where = new Where { MethodName = nameof(TestMethod) },
            Tags = [],
        };
        var processor = new ProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.ShouldProcess(in probeData);

        Assert.True(shouldProcess);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(1, perProbeSampler.SampleCallCount);
    }

    private static CaptureInfo<object> CreateAsyncEvaluateCaptureInfo()
    {
        return new CaptureInfo<object>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryAsync,
            value: new object(),
            method: typeof(ProbeProcessorTests).GetMethod(nameof(TestMethod), BindingFlags.NonPublic | BindingFlags.Static)!,
            invocationTargetType: typeof(ProbeProcessorTests),
            memberKind: ScopeMemberKind.Argument,
            type: typeof(object),
            name: "argument",
            localsCount: 0,
            argumentsCount: 0,
            asyncCaptureInfo: new AsyncCaptureInfo(
                moveNextInvocationTarget: new object(),
                kickoffInvocationTarget: new object(),
                kickoffInvocationTargetType: typeof(object),
                hoistedArgs: [],
                hoistedLocals: []));
    }

    private static LogProbe CreateLogProbe(string probeId, bool captureSnapshot)
    {
        return new LogProbe
        {
            Id = probeId,
            CaptureSnapshot = captureSnapshot,
            EvaluateAt = EvaluateAt.Entry,
            Where = new Where { MethodName = nameof(TestMethod) },
            Tags = [],
        };
    }

    private static LogProbe CreateConditionalLogProbe(string probeId, string conditionJson, bool captureSnapshot)
    {
        var probe = CreateLogProbe(probeId, captureSnapshot);
        probe.When = new SnapshotSegment(dsl: string.Empty, json: conditionJson, str: null);
        return probe;
    }

    private static void TestMethod()
    {
    }

    private sealed class GlobalRateLimiterMock : IDebuggerGlobalRateLimiter
    {
        private readonly Queue<bool> _results = new();

        public GlobalRateLimiterMock(params bool[] results)
        {
            foreach (var result in results)
            {
                _results.Enqueue(result);
            }
        }

        public int ShouldSampleCallCount { get; private set; }

        public string LastProbeId { get; private set; } = string.Empty;

        public ProbeType? LastProbeType { get; private set; }

        public bool ShouldSample(ProbeType probeType, string probeId)
        {
            ShouldSampleCallCount++;
            LastProbeType = probeType;
            LastProbeId = probeId;
            return _results.Count == 0 || _results.Dequeue();
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

    private sealed class AdaptiveSamplerMock : IAdaptiveSampler
    {
        private readonly Queue<bool> _results = new();

        public AdaptiveSamplerMock(params bool[] results)
        {
            foreach (var result in results)
            {
                _results.Enqueue(result);
            }
        }

        public int SampleCallCount { get; private set; }

        public bool Sample()
        {
            SampleCallCount++;
            return _results.Count == 0 || _results.Dequeue();
        }

        public bool Keep() => true;

        public bool Drop() => false;

        public double NextDouble() => 0;

        public void Dispose()
        {
        }
    }
}
