// <copyright file="DebuggerSnapshotSerializerNullableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Telemetry.Metrics;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

// A boxed T? is a boxed T, but the declared type is still Nullable<T>.
// Walking Nullable<T>'s own fields over a boxed T throws FieldAccessException (.NET 9+) or NotSupportedException (older runtimes).
public class DebuggerSnapshotSerializerNullableTests
{
    [Fact]
    public void Capture_NonNullNullableStructArgument_CapturesStructFields()
    {
        Point? point = new Point(7, 9);

        var (snapshot, incompleteReasons) = Capture(point, typeof(Point?));

        Assert.Contains("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}", snapshot);
        Assert.Contains("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}", snapshot);
        Assert.DoesNotContain("hasValue", snapshot);
        Assert.False(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError));
    }

    [Fact]
    public void Capture_NonNullNullableStructField_CapturesStructFields()
    {
        var holder = new PointHolder { Point = new Point(7, 9) };

        var (snapshot, incompleteReasons) = Capture(holder, typeof(PointHolder));

        Assert.Contains("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}", snapshot);
        Assert.Contains("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}", snapshot);
        Assert.DoesNotContain("hasValue", snapshot);
        Assert.False(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError));
    }

    [Fact]
    public void Capture_NullNullableStructArgument_CapturesIsNull()
    {
        Point? point = null;

        var (snapshot, incompleteReasons) = Capture(point, typeof(Point?));

        Assert.Contains("\"isNull\":\"true\"", snapshot);
        Assert.False(HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError));
    }

    private static (string Snapshot, uint IncompleteReasons) Capture<T>(T value, Type declaredType)
    {
        var limitInfo = new CaptureLimitInfo(
            MaxReferenceDepth: 3,
            MaxCollectionSize: 100,
            MaxLength: 255,
            MaxFieldCount: 20);

        var snapshotCreator = new DebuggerSnapshotCreator(
            isFullSnapshot: true,
            ProbeLocation.Method,
            hasCondition: false,
            tags: [],
            limitInfo,
            processTagsProvider: static () => null,
            serviceNameProvider: static () => "test-service");

        snapshotCreator.StartEntry();
        snapshotCreator.CaptureArgument(value, "arg", declaredType);
        snapshotCreator.EndEntry();
        snapshotCreator.FinalizeSnapshot("Foo", "Bar", "foo");
        return (snapshotCreator.GetSnapshotJson(), snapshotCreator.IncompleteReasons);
    }

    private static bool HasReason(uint incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason reason)
        => (incompleteReasons & (1u << (int)reason)) != 0;

    private readonly struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    private sealed class PointHolder
    {
        public Point? Point { get; set; }
    }
}
