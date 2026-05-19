// <copyright file="ProbeProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeProcessorTests
{
    private const string InvalidConditionJson = @"{
    ""gt"": [
      {""ref"": ""undefined""},
      2
    ]
}";

    [Fact]
    public void ConditionEvaluationErrorsAreDroppedWhenSamplerRejects()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.False(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorsAreCapturedWhenSamplerKeeps()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationExceptionsAreDroppedWhenSamplerRejects()
    {
        var processor = CreateConditionalProbeProcessor();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        var snapshotCreator = CreateSnapshotCreator(processor, in probeData);
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        ClearMethodScopeMembers(snapshotCreator);
        Assert.False(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
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

    private static ProbeProcessor CreateConditionalProbeProcessor()
    {
        return new ProbeProcessor(
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
        return new ProbeProcessor(
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
        return new ProbeProcessor(CreateVersionedCaptureExpressionProbe(probeId, version, captureName));
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
        return new ProbeProcessor(
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
}
