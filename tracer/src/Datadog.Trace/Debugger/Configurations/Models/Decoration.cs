// <copyright file="Decoration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially

#nullable enable
using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Configurations.Models;

internal sealed record Decoration
{
    public SnapshotSegment? When { get; set; }

    public Tags[]? Tags { get; set; }

    public bool Equals(Decoration? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(When, other.When) && Tags.NullableSequentialEquals(other.Tags);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(When, Tags.NullableSequentialHashCode());
    }
}

internal sealed record Tags
{
    public string? Name { get; set; }

    public TagValue? Value { get; set; }
}

internal sealed record TagValue
{
    public string? Template { get; set; }

    public SnapshotSegment[]? Segments { get; set; }

    public bool Equals(TagValue? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Template == other.Template && Segments.NullableSequentialEquals(other.Segments);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Template, Segments.NullableSequentialHashCode());
    }
}

#pragma warning restore SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially
