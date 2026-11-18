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
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DebuggerSnapshotSerializerNullableTests
{
    [Fact]
    public void Capture_NonNullNullableStructArgument_CapturesStructFields()
    {
        Point? point = new Point(7, 9);

        var (snapshot, incompleteReasons) = Capture(point, typeof(Point?));

        snapshot.Should().Contain("\"type\":\"Nullable`1\"");
        snapshot.Should().Contain("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}");
        snapshot.Should().Contain("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}");
        snapshot.Should().NotContain("hasValue");
        HasRuntimeError(incompleteReasons).Should().BeFalse();
    }

    [Fact]
    public void Capture_NonNullNullableStructField_CapturesStructFields()
    {
        var holder = new PointHolder { Point = new Point(7, 9) };

        var (snapshot, incompleteReasons) = Capture(holder, typeof(PointHolder));

        snapshot.Should().Contain("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}");
        snapshot.Should().Contain("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}");
        snapshot.Should().NotContain("hasValue");
        HasRuntimeError(incompleteReasons).Should().BeFalse();
    }

    [Fact]
    public void Capture_NullNullableStructArgument_CapturesIsNull()
    {
        var (snapshot, incompleteReasons) = Capture(null, typeof(Point?));

        snapshot.Should().Contain("\"isNull\":\"true\"");
        HasRuntimeError(incompleteReasons).Should().BeFalse();
    }

    [Fact]
    public void Capture_NonNullNullableStructWithSensitiveField_RedactsField()
    {
        Credentials? credentials = new Credentials("hunter2");

        var (snapshot, incompleteReasons) = Capture(credentials, typeof(Credentials?));

        snapshot.Should().Contain("\"Password\":{\"type\":\"String\",\"notCapturedReason\":\"redactedIdent\"}");
        snapshot.Should().NotContain("hunter2");
        HasRuntimeError(incompleteReasons).Should().BeFalse();
    }

    private static (string Snapshot, uint IncompleteReasons) Capture(object? value, Type declaredType)
    {
        var snapshotCreator = new DebuggerSnapshotCreator(
            isFullSnapshot: true,
            ProbeLocation.Method,
            hasCondition: false,
            tags: [],
            new CaptureLimitInfo(MaxReferenceDepth: 3, MaxCollectionSize: 100, MaxLength: 255, MaxFieldCount: 20),
            processTagsProvider: static () => null,
            serviceNameProvider: static () => "test-service");

        snapshotCreator.StartEntry();
        snapshotCreator.CaptureArgument(value, "arg", declaredType);
        snapshotCreator.EndEntry();
        snapshotCreator.FinalizeSnapshot("Foo", "Bar", "foo");
        return (snapshotCreator.GetSnapshotJson(), snapshotCreator.IncompleteReasons);
    }

    private static bool HasRuntimeError(uint incompleteReasons)
        => (incompleteReasons & (1u << (int)MetricTags.DebuggerCaptureIncompleteReason.RuntimeError)) != 0;

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

    private readonly struct Credentials
    {
        public Credentials(string password) => Password = password;

        public string Password { get; }
    }

    private sealed class PointHolder
    {
        public Point? Point { get; set; }
    }
}
