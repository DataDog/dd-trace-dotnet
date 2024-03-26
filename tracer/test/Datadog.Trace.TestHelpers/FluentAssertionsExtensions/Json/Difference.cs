// <copyright file="Difference.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

internal class Difference
{
    public Difference(DifferenceKind kind, JPath path, object actual, object expected)
        : this(kind, path)
    {
        Actual = actual;
        Expected = expected;
    }

    public Difference(DifferenceKind kind, JPath path)
    {
        Kind = kind;
        Path = path;
    }

    private DifferenceKind Kind { get; }

    private JPath Path { get; }

    private object Actual { get; }

    private object Expected { get; }

    public override string ToString()
    {
        return Kind switch
        {
            DifferenceKind.ActualIsNull => "is null",
            DifferenceKind.ExpectedIsNull => "is not null",
            DifferenceKind.OtherType => $"has {Actual} instead of {Expected} at {Path}",
            DifferenceKind.OtherName => $"has a different name at {Path}",
            DifferenceKind.OtherValue => $"has a different value at {Path}",
            DifferenceKind.DifferentLength => $"has {Actual} elements instead of {Expected} at {Path}",
            DifferenceKind.ActualMissesProperty => $"misses property {Path}",
            DifferenceKind.ExpectedMissesProperty => $"has extra property {Path}",
            DifferenceKind.ActualMissesElement => $"misses expected element {Path}",
            DifferenceKind.WrongOrder => $"has expected element {Path} in the wrong order",
#pragma warning disable S3928 // Parameter names used into ArgumentException constructors should match an existing one
            _ => throw new ArgumentOutOfRangeException(),
#pragma warning restore S3928 // Parameter names used into ArgumentException constructors should match an existing one
        };
    }
}
