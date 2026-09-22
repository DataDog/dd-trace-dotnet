// <copyright file="DebuggerSnapshotSerializerGuardrailTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Telemetry.Metrics;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DebuggerSnapshotSerializerGuardrailTests
{
    [Fact]
    public void Capture_WhenDepthLimitApplies_MarksDepth()
    {
        var (snapshot, incompleteReasons) = Capture(new WideDepthTarget(), maxReferenceDepth: 1);

        Assert.Contains("\"notCapturedReason\":\"depth\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.Depth));
    }

    [Fact]
    public void Capture_WhenCollectionSizeLimitApplies_MarksCollectionSize()
    {
        var (snapshot, incompleteReasons) = Capture(new List<int> { 1, 2, 3, 4 }, maxCollectionSize: 1);

        Assert.Contains("\"notCapturedReason\":\"collectionSize\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.CollectionSize));
    }

    [Fact]
    public void Capture_WhenStringLengthLimitApplies_MarksStringLength()
    {
        var (snapshot, incompleteReasons) = Capture(new string('f', 50), maxLength: 4);

        Assert.Contains("\"value\":\"ffff\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.StringLength));
    }

    [Fact]
    public void Capture_WhenFieldCountLimitApplies_MarksFieldCount()
    {
        var (snapshot, incompleteReasons) = Capture(new ManyFieldsTarget(), maxFieldCount: 1);

        Assert.Contains("\"notCapturedReason\":\"fieldCount\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.FieldCount));
    }

    [Fact]
    public void Capture_WhenEnumerableThrows_MarksRuntimeError()
    {
        var (snapshot, incompleteReasons) = Capture(new ThrowingCaptureCollection());

        Assert.Contains("\"elements\":[]", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError));
    }

    [Fact]
    public void Capture_WhenMemberGetterThrows_MarksRuntimeError()
    {
        var (snapshot, incompleteReasons) = Capture(new ThrowingMessageException());

        Assert.Contains("\"type\":\"ThrowingMessageException\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError));
    }

    [Fact]
    public void Capture_WhenAsyncLocalIsUnreachable_MarksOther()
    {
        var (snapshot, incompleteReasons) = Capture(new DebuggerSnapshotSerializer.UnreachableLocal("unreachable"));

        Assert.Contains("\"notCapturedReason\":\"unreachable\"", snapshot);
        Assert.True(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.Other));
    }

    private static (string Snapshot, uint IncompleteReasons) Capture(
        object value,
        int maxReferenceDepth = 3,
        int maxCollectionSize = 100,
        int maxLength = 255,
        int maxFieldCount = 20)
    {
        var snapshotCreator = CreateCreator(maxReferenceDepth, maxCollectionSize, maxLength, maxFieldCount);
        CaptureInto(snapshotCreator, value);
        return (snapshotCreator.GetSnapshotJson(), snapshotCreator.IncompleteReasons);
    }

    private static DebuggerSnapshotCreator CreateCreator(
        int maxReferenceDepth = 3,
        int maxCollectionSize = 100,
        int maxLength = 255,
        int maxFieldCount = 20)
    {
        var limitInfo = new CaptureLimitInfo(
            MaxReferenceDepth: maxReferenceDepth,
            MaxCollectionSize: maxCollectionSize,
            MaxLength: maxLength,
            MaxFieldCount: maxFieldCount);

        return new DebuggerSnapshotCreator(
            isFullSnapshot: true,
            ProbeLocation.Method,
            hasCondition: false,
            tags: [],
            limitInfo,
            processTagsProvider: static () => null,
            serviceNameProvider: static () => "test-service");
    }

    private static void CaptureInto(DebuggerSnapshotCreator snapshotCreator, object value)
    {
        snapshotCreator.StartEntry();
        snapshotCreator.CaptureArgument(value, "arg", value.GetType());
        snapshotCreator.EndEntry();
        snapshotCreator.FinalizeSnapshot("Foo", "Bar", "foo");
    }

    private static bool HasReason(uint incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason reason)
        => (incompleteReasons & (1u << (int)reason)) != 0;

#pragma warning disable CS0414
    private sealed class WideDepthTarget
    {
        private NestedTarget _first = new();
        private NestedTarget _second = new();
        private NestedTarget _third = new();
    }

    private sealed class NestedTarget
    {
        private int _value = 1;
    }

    private sealed class ManyFieldsTarget
    {
        private int _one = 1;
        private int _two = 2;
        private int _three = 3;
    }

    private sealed class ThrowingCaptureCollection : IEnumerable, IBoundedCaptureCollectionResult
    {
        public int Count => 1;

        public bool WasTruncated => false;

        public bool IsDictionary => false;

        public IEnumerator GetEnumerator() => new ThrowingEnumerator();

        private sealed class ThrowingEnumerator : IEnumerator
        {
            public object Current => throw new InvalidOperationException("Current failed");

            public bool MoveNext() => throw new InvalidOperationException("MoveNext failed");

            public void Reset()
            {
            }
        }
    }

    private sealed class ThrowingMessageException : Exception
    {
        public override string Message => throw new InvalidOperationException("Message failed");
    }
#pragma warning restore CS0414
}
