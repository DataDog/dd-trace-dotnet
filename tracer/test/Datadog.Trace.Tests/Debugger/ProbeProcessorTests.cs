// <copyright file="ProbeProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeProcessorTests
{
    private const int DefaultMaxEvaluationTimeInMilliseconds = DebuggerSettings.DefaultMaxEvaluationTimeInMilliseconds;

    private const string InvalidConditionJson = @"{
    ""gt"": [
      {""ref"": ""undefined""},
      2
    ]
}";

    private const string FalseConditionJson = @"{ ""eq"": [1, 0] }";

    private const string UpdatedInvalidConditionJson = @"{
    ""gt"": [
      {""ref"": ""updatedUndefined""},
      2
    ]
}";

    [Fact]
    public void ConditionEvaluationErrorsBypassSampler()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorsAreCapturedWithoutSampler()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void UsesConfiguredEvaluationTimeBudget()
    {
        const int maxEvaluationTimeInMilliseconds = 123;
        var processor = new ProbeProcessor(CreateConditionalLogProbe("probe-id", FalseConditionJson, captureSnapshot: false), maxEvaluationTimeInMilliseconds);
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        ProcessEntryEnd(processor, snapshotCreator, in probeData, method);

        Assert.Equal(maxEvaluationTimeInMilliseconds, GetEvaluator(processor).MaxEvaluationTimeInMilliseconds);
    }

    [Fact]
    public void ConditionEvaluationErrorsFinalizeWithoutCaptureData()
    {
        var processor = CreateProbeProcessor(CreateConditionalLogProbe("probe-id", InvalidConditionJson, captureSnapshot: true));
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));

        var snapshot = JObject.Parse(FinalizeMethodSnapshot(snapshotCreator, "probe-id", method));

        Assert.NotEmpty(snapshot.SelectToken("debugger.snapshot.evaluationErrors")!);
        Assert.False(CapturesContainData(snapshot.SelectToken("debugger.snapshot.captures")));
        Assert.NotNull(snapshot.SelectToken("debugger.snapshot.stack"));
    }

    [Fact]
    public void UnconditionalFullSnapshotWithoutExpressionsDoesNotEvaluate()
    {
        var processor = CreateProbeProcessor(CreateLogProbe("probe-id", captureSnapshot: true));
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));

        var snapshot = JObject.Parse(FinalizeMethodSnapshot(snapshotCreator, "probe-id", method));

        Assert.Null(snapshot.SelectToken("debugger.snapshot.evaluationErrors"));
    }

    [Fact]
    public void ConditionEvaluationExceptionsBypassSampler()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        ClearMethodScopeMembers(snapshotCreator);
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorsAreRateLimitedWithoutSampler()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(true, true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var firstSnapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, firstSnapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, firstSnapshotCreator, in probeData, method));

        var secondSnapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, secondSnapshotCreator, in probeData, method));
        Assert.False(ProcessEntryEnd(processor, secondSnapshotCreator, in probeData, method));

        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorSamplerRejectionDoesNotBlockFirstSnapshot()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var firstSnapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, firstSnapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, firstSnapshotCreator, in probeData, method));

        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorSnapshotBypassesGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var sampler = new TestAdaptiveSampler(false);
        var probe = CreateConditionalLogProbe("snapshot-probe", InvalidConditionJson, captureSnapshot: true);
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));

        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorRateLimitResetsOnProbeUpdate()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(true, true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var firstSnapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, firstSnapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, firstSnapshotCreator, in probeData, method));

        processor.UpdateProbeProcessor(CreateConditionalLogProbe("probe-id", UpdatedInvalidConditionJson, captureSnapshot: false));

        var secondSnapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.True(ProcessEntryStart(processor, secondSnapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, secondSnapshotCreator, in probeData, method));

        Assert.Equal(0, sampler.SampleCalls);
    }

    [Fact]
    public void CaptureExpressionOnlyProbeEvaluatesAndSerializesResults()
    {
        var processor = CreateCaptureExpressionProbeProcessor();
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.ExecuteWithValue))!;

        Assert.True(ProcessExitStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessLogArg(processor, snapshotCreator, in probeData, method, "inputValue", "testValue"));
        Assert.True(ProcessLogLocal(processor, snapshotCreator, in probeData, method, "localValue", 9));
        Assert.True(ProcessExitEnd(processor, snapshotCreator, in probeData, method));
    }

    [Fact]
    public void CaptureExpressionOnlyProbeIgnoresNullCaptureExpressionEntries()
    {
        var processor = CreateCaptureExpressionProbeProcessor(includeNullEntry: true);
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.ExecuteWithValue))!;

        Assert.True(ProcessExitStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessLogArg(processor, snapshotCreator, in probeData, method, "inputValue", "testValue"));
        Assert.True(ProcessExitEnd(processor, snapshotCreator, in probeData, method));
    }

    [Fact]
    public void CaptureExpressionOnlyProbeIgnoresEmptyNameCaptureExpressionEntries()
    {
        var processor = CreateCaptureExpressionProbeProcessor(includeEmptyNameEntry: true);
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.ExecuteWithValue))!;

        Assert.True(ProcessExitStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessLogArg(processor, snapshotCreator, in probeData, method, "inputValue", "testValue"));
        Assert.True(ProcessExitEnd(processor, snapshotCreator, in probeData, method));
    }

    [Fact]
    public void CaptureExpressionOnlyProbeDropsSnapshotWhenNoExpressionsCaptureValues()
    {
        var processor = CreateUndefinedCaptureExpressionProbeProcessor();
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.ExecuteWithValue))!;

        Assert.True(ProcessExitStart(processor, snapshotCreator, in probeData, method));
        Assert.False(ProcessExitEnd(processor, snapshotCreator, in probeData, method));
        // TryBeginProcess is the production entry gate, so non-conditional probes sample before capture-expression evaluation can drop the snapshot.
        Assert.Equal(1, sampler.SampleCalls);
    }

    [Fact]
    public void SnapshotCreatorKeepsPublishedStateAfterProcessorUpdate()
    {
        var processor = CreateVersionedCaptureExpressionProbeProcessor("probe-id", version: 1, captureName: "inputValue");
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.ExecuteWithValue))!;

        processor.UpdateProbeProcessor(CreateVersionedCaptureExpressionProbe("probe-id", version: 2, captureName: "missingValue"));

        Assert.True(ProcessExitStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessLogArg(processor, snapshotCreator, in probeData, method, "inputValue", "testValue"));
        Assert.True(ProcessExitEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, snapshotCreator.ProbeProcessorState!.ProbeInfo.ProbeVersion);
        Assert.Equal("probe-id", snapshotCreator.ProbeProcessorState.ProbeInfo.ProbeId);
    }

    [Fact]
    public void TryBeginProcess_UnconditionalSnapshotProbe_SamplesGlobalBeforePerProbe()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new TestAdaptiveSampler(true);
        var probe = CreateLogProbe("snapshot-probe", captureSnapshot: true);
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.TryBeginProcess(in probeData, out var snapshotCreator);

        Assert.False(shouldProcess);
        Assert.Null(snapshotCreator);
        Assert.Equal(1, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal("snapshot-probe", globalRateLimiter.LastProbeId);
        Assert.Equal(0, perProbeSampler.SampleCalls);
    }

    [Fact]
    public void TryBeginProcess_UnconditionalSnapshotProbe_CalibratesGlobalSamplerWhenPerProbeRejects()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(true);
        var perProbeSampler = new TestAdaptiveSampler(false);
        var probe = CreateLogProbe("snapshot-probe", captureSnapshot: true);
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.TryBeginProcess(in probeData, out var snapshotCreator);

        Assert.False(shouldProcess);
        Assert.Null(snapshotCreator);
        Assert.Equal(1, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal("snapshot-probe", globalRateLimiter.LastProbeId);
        Assert.Equal(1, perProbeSampler.SampleCalls);
    }

    [Fact]
    public void TryBeginProcess_UnconditionalLogProbe_DoesNotUseGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new TestAdaptiveSampler(true);
        var probe = CreateLogProbe("log-probe", captureSnapshot: false);
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.TryBeginProcess(in probeData, out var snapshotCreator);

        Assert.True(shouldProcess);
        Assert.NotNull(snapshotCreator);
        Assert.Equal(1, perProbeSampler.SampleCalls);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
    }

    [Fact]
    public void Process_ConditionalProbe_EvaluatesConditionBeforeRateLimiting()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new TestAdaptiveSampler(true);
        var probe = CreateConditionalLogProbe("conditional-false", FalseConditionJson, captureSnapshot: true);
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);
        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        var captureInfo = CreateAsyncEvaluateCaptureInfo();

        var result = processor.Process(ref captureInfo, snapshotCreator, in probeData);

        Assert.False(result);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(0, perProbeSampler.SampleCalls);
    }

    [Fact]
    public void TryBeginProcess_MetricProbe_DoesNotUseGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new TestAdaptiveSampler(true);
        var probe = new MetricProbe
        {
            Id = "metric-probe",
            MetricName = "metric",
            Kind = MetricKind.COUNT,
            Where = new Where { MethodName = nameof(SampleTarget.Execute) },
            Tags = [],
        };
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.TryBeginProcess(in probeData, out var snapshotCreator);

        Assert.True(shouldProcess);
        Assert.NotNull(snapshotCreator);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(1, perProbeSampler.SampleCalls);
    }

    [Fact]
    public void TryBeginProcess_SpanDecorationProbe_DoesNotUseGlobalLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock(false);
        var perProbeSampler = new TestAdaptiveSampler(true);
        var probe = new SpanDecorationProbe
        {
            Id = "span-probe",
            Decorations = [],
            TargetSpan = TargetSpan.Active,
            Where = new Where { MethodName = nameof(SampleTarget.Execute) },
            Tags = [],
        };
        var processor = CreateProbeProcessor(probe, globalRateLimiter);
        var probeData = new ProbeData(probe.Id, perProbeSampler, processor);

        var shouldProcess = processor.TryBeginProcess(in probeData, out var snapshotCreator);

        Assert.True(shouldProcess);
        Assert.NotNull(snapshotCreator);
        Assert.Equal(0, globalRateLimiter.ShouldSampleCallCount);
        Assert.Equal(1, perProbeSampler.SampleCalls);
    }

    private static ProbeProcessor CreateConditionalProbeProcessor()
    {
        return CreateProbeProcessor(
            new LogProbe
            {
                Id = "probe-id",
                CaptureSnapshot = false,
                EvaluateAt = EvaluateAt.Entry,
                Tags = [],
                Where = new Where
                {
                    TypeName = typeof(SampleTarget).FullName!,
                    MethodName = nameof(SampleTarget.Execute)
                },
                When = new SnapshotSegment(dsl: string.Empty, json: InvalidConditionJson, str: string.Empty)
            });
    }

    private static ProbeProcessor CreateCaptureExpressionProbeProcessor(bool includeNullEntry = false, bool includeEmptyNameEntry = false)
    {
        return CreateProbeProcessor(
            new LogProbe
            {
                Id = "probe-id",
                CaptureSnapshot = false,
                EvaluateAt = EvaluateAt.Exit,
                Tags = [],
                Where = new Where
                {
                    TypeName = typeof(SampleTarget).FullName!,
                    MethodName = nameof(SampleTarget.ExecuteWithValue)
                },
                CaptureExpressions =
                    includeNullEntry
                        ? [null, new CaptureExpression { Name = "inputValue", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""inputValue""}", null) }]
                        : includeEmptyNameEntry
                            ? [new CaptureExpression { Name = string.Empty, Expr = new SnapshotSegment(string.Empty, @"{""ref"":""localValue""}", null) }, new CaptureExpression { Name = "inputValue", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""inputValue""}", null) }]
                        : [
                            new CaptureExpression { Name = "inputValue", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""inputValue""}", null) },
                            new CaptureExpression { Name = "localValue", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""localValue""}", null) }
                        ]
            });
    }

    private static ProbeProcessor CreateVersionedCaptureExpressionProbeProcessor(string probeId, int version, string captureName)
    {
        return CreateProbeProcessor(CreateVersionedCaptureExpressionProbe(probeId, version, captureName));
    }

    private static ProbeProcessor CreateProbeProcessor(ProbeDefinition probe)
    {
        return new ProbeProcessor(probe, DefaultMaxEvaluationTimeInMilliseconds);
    }

    private static ProbeProcessor CreateProbeProcessor(ProbeDefinition probe, IDebuggerGlobalRateLimiter globalRateLimiter)
    {
        return new ProbeProcessor(probe, DefaultMaxEvaluationTimeInMilliseconds, globalRateLimiter);
    }

    private static LogProbe CreateLogProbe(string probeId, bool captureSnapshot)
    {
        return new LogProbe
        {
            Id = probeId,
            CaptureSnapshot = captureSnapshot,
            EvaluateAt = EvaluateAt.Entry,
            Where = new Where
            {
                TypeName = typeof(SampleTarget).FullName!,
                MethodName = nameof(SampleTarget.Execute)
            },
            Tags = [],
        };
    }

    private static LogProbe CreateConditionalLogProbe(string probeId, string conditionJson, bool captureSnapshot)
    {
        var probe = CreateLogProbe(probeId, captureSnapshot);
        probe.When = new SnapshotSegment(dsl: string.Empty, json: conditionJson, str: null);
        return probe;
    }

    private static CaptureInfo<object> CreateAsyncEvaluateCaptureInfo()
    {
        return new CaptureInfo<object>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryAsync,
            value: new object(),
            method: typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!,
            invocationTargetType: typeof(object),
            memberKind: ScopeMemberKind.Argument,
            type: typeof(object),
            name: "argument",
            localsCount: 0,
            argumentsCount: 0,
            asyncCaptureInfo: new AsyncCaptureInfo(
                moveNextInvocationTarget: new object(),
                kickoffInvocationTarget: new object(),
                kickoffInvocationTargetType: typeof(SampleTarget),
                hoistedArgs: [],
                hoistedLocals: []));
    }

    private static LogProbe CreateVersionedCaptureExpressionProbe(string probeId, int version, string captureName)
    {
        return new LogProbe
        {
            Id = probeId,
            Version = version,
            CaptureSnapshot = false,
            EvaluateAt = EvaluateAt.Exit,
            Tags = [],
            Where = new Where
            {
                TypeName = typeof(SampleTarget).FullName!,
                MethodName = nameof(SampleTarget.ExecuteWithValue)
            },
            CaptureExpressions =
            [
                new CaptureExpression { Name = captureName, Expr = new SnapshotSegment(string.Empty, @"{""ref"":""inputValue""}", null) }
            ]
        };
    }

    private static ProbeProcessor CreateUndefinedCaptureExpressionProbeProcessor()
    {
        return CreateProbeProcessor(
            new LogProbe
            {
                Id = "probe-id",
                CaptureSnapshot = false,
                EvaluateAt = EvaluateAt.Exit,
                Tags = [],
                Where = new Where
                {
                    TypeName = typeof(SampleTarget).FullName!,
                    MethodName = nameof(SampleTarget.ExecuteWithValue)
                },
                CaptureExpressions =
                [
                    new CaptureExpression { Name = "missingValue" }
                ]
            });
    }

    private static DebuggerSnapshotCreator CreateSnapshotCreator(ProbeProcessor processor, in ProbeData probeData)
    {
        Assert.True(processor.TryBeginProcess(in probeData, out var snapshotCreator));
        return (DebuggerSnapshotCreator)snapshotCreator;
    }

    private static bool ProcessEntryStart(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method)
    {
        var captureInfo = new CaptureInfo<Type>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryStart,
            method: method,
            type: method.DeclaringType!,
            invocationTargetType: method.DeclaringType!,
            localsCount: 0,
            argumentsCount: 0);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static bool ProcessEntryEnd(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method)
    {
        var target = new SampleTarget();
        var captureInfo = new CaptureInfo<SampleTarget>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryEnd,
            value: target,
            method: method,
            type: target.GetType(),
            invocationTargetType: target.GetType(),
            memberKind: ScopeMemberKind.This,
            hasLocalOrArgument: true);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static bool ProcessExitStart(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method)
    {
        var captureInfo = new CaptureInfo<string>(
            methodMetadataIndex: 0,
            methodState: MethodState.ExitStart,
            value: "result",
            name: "@return",
            method: method,
            type: typeof(string),
            invocationTargetType: method.DeclaringType!,
            memberKind: ScopeMemberKind.Return,
            localsCount: 1,
            argumentsCount: 1);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static bool ProcessLogArg<T>(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method, string name, T value)
    {
        var captureInfo = new CaptureInfo<T>(
            methodMetadataIndex: 0,
            methodState: MethodState.LogArg,
            name: name,
            value: value,
            method: method,
            type: value?.GetType() ?? typeof(T),
            invocationTargetType: method.DeclaringType!,
            memberKind: ScopeMemberKind.Argument);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static bool ProcessLogLocal<T>(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method, string name, T value)
    {
        var captureInfo = new CaptureInfo<T>(
            methodMetadataIndex: 0,
            methodState: MethodState.LogLocal,
            name: name,
            value: value,
            method: method,
            type: value?.GetType() ?? typeof(T),
            invocationTargetType: method.DeclaringType!,
            memberKind: ScopeMemberKind.Local);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static bool ProcessExitEnd(ProbeProcessor processor, DebuggerSnapshotCreator snapshotCreator, in ProbeData probeData, MethodInfo method)
    {
        var captureInfo = new CaptureInfo<SampleTarget>(
            methodMetadataIndex: 0,
            methodState: MethodState.ExitEnd,
            value: new SampleTarget(),
            method: method,
            type: typeof(SampleTarget),
            invocationTargetType: typeof(SampleTarget),
            memberKind: ScopeMemberKind.This);

        return processor.Process(ref captureInfo, snapshotCreator, in probeData);
    }

    private static void ClearMethodScopeMembers(DebuggerSnapshotCreator snapshotCreator)
    {
        typeof(DebuggerSnapshotCreator)
           .GetProperty(nameof(DebuggerSnapshotCreator.MethodScopeMembers), BindingFlags.Instance | BindingFlags.NonPublic)!
           .SetValue(snapshotCreator, null);
    }

    private static string FinalizeMethodSnapshot(DebuggerSnapshotCreator snapshotCreator, string probeId, MethodInfo method)
    {
        var captureInfo = new CaptureInfo<SampleTarget>(
            methodMetadataIndex: 0,
            methodState: MethodState.EntryEnd,
            value: new SampleTarget(),
            method: method,
            type: typeof(SampleTarget),
            invocationTargetType: typeof(SampleTarget),
            memberKind: ScopeMemberKind.This);

        return snapshotCreator.FinalizeMethodSnapshot(probeId, 0, ref captureInfo);
    }

    private static ProbeExpressionEvaluator GetEvaluator(ProbeProcessor processor)
    {
        var state = typeof(ProbeProcessor)
                   .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
                   .GetValue(processor)!;

        return (ProbeExpressionEvaluator)state
                                        .GetType()
                                        .GetField("_evaluator", BindingFlags.Instance | BindingFlags.NonPublic)!
                                        .GetValue(state)!;
    }

    private static bool CapturesContainData(JToken captures)
    {
        if (captures is not JObject capturesObject)
        {
            return false;
        }

        foreach (var property in capturesObject.Properties())
        {
            if (property.Name == "lines" && property.Value is JObject lines)
            {
                foreach (var lineProperty in lines.Properties())
                {
                    if (CapturePointContainsData(lineProperty.Value))
                    {
                        return true;
                    }
                }
            }
            else if (CapturePointContainsData(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CapturePointContainsData(JToken capturePoint)
    {
        if (capturePoint is not JObject capturePointObject)
        {
            return false;
        }

        return HasContent(capturePointObject["arguments"])
            || HasContent(capturePointObject["locals"])
            || HasContent(capturePointObject["staticFields"])
            || HasContent(capturePointObject["throwable"]);
    }

    private static bool HasContent(JToken token)
    {
        return token != null && token.HasValues;
    }

    private sealed class SampleTarget
    {
        public void Execute()
        {
        }

        public string ExecuteWithValue(string inputValue)
        {
            var localValue = inputValue.Length;
            return localValue.ToString();
        }
    }

    private sealed class TestAdaptiveSampler(params bool[] samples) : IAdaptiveSampler
    {
        private readonly bool[] _samples = samples.Length == 0 ? [true] : samples;
        private int _sampleIndex;

        public int SampleCalls { get; private set; }

        public bool Sample()
        {
            SampleCalls++;
            var index = Math.Min(_sampleIndex, _samples.Length - 1);
            _sampleIndex++;
            return _samples[index];
        }

        public bool Keep() => Sample();

        public bool Drop() => !Sample();

        public double NextDouble() => 0;

        public void Dispose()
        {
        }
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

        public bool ShouldSampleSnapshot(string probeId)
        {
            ShouldSampleCallCount++;
            LastProbeId = probeId;
            return _results.Count == 0 || _results.Dequeue();
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
