// <copyright file="DebuggerSnapshotSerializerNullableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;
using static Datadog.Trace.Tests.Debugger.DebuggerSnapshotSerializerGuardrailTests;

namespace Datadog.Trace.Tests.Debugger;

// A boxed T? is a boxed T, but the declared type is still Nullable<T>.
// Walking Nullable<T>'s own fields over a boxed T throws FieldAccessException (.NET 9+) or NotSupportedException (older runtimes).
public class DebuggerSnapshotSerializerNullableTests
{
    [Fact]
    public void Capture_NonNullNullableStructArgument_CapturesStructFields()
    {
        Point? point = new Point(7, 9);

        var (snapshot, incompleteReasons) = Capture(point, declaredType: typeof(Point?));

        snapshot.Should().Contain("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}");
        snapshot.Should().Contain("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}");
        snapshot.Should().NotContain("hasValue");
        HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError).Should().BeFalse();
    }

    [Fact]
    public void Capture_NonNullNullableStructField_CapturesStructFields()
    {
        var holder = new PointHolder { Point = new Point(7, 9) };

        var (snapshot, incompleteReasons) = Capture(holder);

        snapshot.Should().Contain("\"X\":{\"type\":\"Int32\",\"value\":\"7\"}");
        snapshot.Should().Contain("\"Y\":{\"type\":\"Int32\",\"value\":\"9\"}");
        snapshot.Should().NotContain("hasValue");
        HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError).Should().BeFalse();
    }

    [Fact]
    public void Capture_NullNullableStructArgument_CapturesIsNull()
    {
        var (snapshot, incompleteReasons) = Capture(null, declaredType: typeof(Point?));

        snapshot.Should().Contain("\"isNull\":\"true\"");
        HasReason(incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.RuntimeError).Should().BeFalse();
    }

    [Fact]
    public void Capture_NonNullNullableStructWithSensitiveField_RedactsField()
    {
        Credentials? credentials = new Credentials("hunter2");

        var (snapshot, _) = Capture(credentials, declaredType: typeof(Credentials?));

        snapshot.Should().Contain("\"Password\":{\"type\":\"String\",\"notCapturedReason\":\"redactedIdent\"}");
        snapshot.Should().NotContain("hunter2");
    }

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
