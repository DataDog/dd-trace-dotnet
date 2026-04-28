// <copyright file="ProbeProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
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
        var snapshotCreator = CreateSnapshotCreator();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        Assert.True(processor.ShouldProcess(in probeData));
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.False(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationErrorsAreCapturedWhenSamplerKeeps()
    {
        var processor = CreateConditionalProbeProcessor();
        var snapshotCreator = CreateSnapshotCreator();
        var sampler = new TestAdaptiveSampler(true);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        Assert.True(processor.ShouldProcess(in probeData));
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        Assert.True(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
    }

    [Fact]
    public void ConditionEvaluationExceptionsAreDroppedWhenSamplerRejects()
    {
        var processor = CreateConditionalProbeProcessor();
        var snapshotCreator = CreateSnapshotCreator();
        var sampler = new TestAdaptiveSampler(false);
        var probeData = new ProbeData("probe-id", sampler, processor);
        var method = typeof(SampleTarget).GetMethod(nameof(SampleTarget.Execute))!;

        Assert.True(processor.ShouldProcess(in probeData));
        Assert.Equal(0, sampler.SampleCalls);

        Assert.True(ProcessEntryStart(processor, snapshotCreator, in probeData, method));
        ClearMethodScopeMembers(snapshotCreator);
        Assert.False(ProcessEntryEnd(processor, snapshotCreator, in probeData, method));
        Assert.Equal(1, sampler.SampleCalls);
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

    private static DebuggerSnapshotCreator CreateSnapshotCreator()
    {
        var captureLimitInfo = new CaptureLimitInfo(
            MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
            MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
            MaxLength: DebuggerSettings.DefaultMaxStringLength,
            MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy);

        return new DebuggerSnapshotCreator(
            isFullSnapshot: false,
            ProbeLocation.Method,
            hasCondition: true,
            tags: [],
            limitInfo: captureLimitInfo,
            processTagsProvider: static () => null,
            serviceNameProvider: static () => "test-service");
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
    }
}
