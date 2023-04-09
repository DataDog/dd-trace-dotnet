// <copyright file="Range.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Iast;

internal readonly struct Range : IComparable<Range>
{
    public Range(int start, int length, Source source)
    {
        this.Start = start;
        this.Length = length;
        this.Source = source;
    }

    public int Start { get; }

    public int Length { get; }

    public Source Source { get; }

    public bool IsEmpty()
    {
        return Length <= 0;
    }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Start, Length, Source);
    }

    public Range Shift(int offset)
    {
        if (offset == 0)
        {
            return this;
        }

        return new Range(Start + offset, Length, Source);
    }

    public int CompareTo([AllowNull] Range other)
    {
        return this.Start.CompareTo(other.Start);
    }
}
