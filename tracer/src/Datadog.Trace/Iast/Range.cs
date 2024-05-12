// <copyright file="Range.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Iast;

internal readonly struct Range : IComparable<Range>
{
    private static readonly SecureMarks NotMarked = SecureMarks.None;

    public Range(int start, int length, Source? source = null, SecureMarks secureMarks = SecureMarks.None)
    {
        this.Start = start;
        this.Length = length;
        this.Source = source;
        this.SecureMarks = secureMarks;
    }

    public int Start { get; }

    public int Length { get; }

    public Source? Source { get; }

    public SecureMarks SecureMarks { get; }

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

        return new Range(Start + offset, Length, Source, SecureMarks);
    }

    public int CompareTo([AllowNull] Range other)
    {
        return this.Start.CompareTo(other.Start);
    }

    public bool IsMarked(SecureMarks marks)
    {
        return (SecureMarks & marks) != NotMarked;
    }

    internal bool IsBefore(Range? range)
    {
        if (range == null)
        {
            return true;
        }

        return IsBefore(range.Value);
    }

    internal bool IsBefore(Range range)
    {
        int offset = Start - range.Start;
        if (offset == 0)
        {
            return Length <= range.Length; // put smaller ranges first
        }

        return offset < 0;
    }

    internal bool Intersects(Range range)
    {
        return range.Start < (Start + Length) && (range.Start + range.Length > Start);
    }

    internal bool Contains(Range range)
    {
        if (Start > range.Start)
        {
            return false;
        }

        return (Start + Length) >= (range.Start + range.Length);
    }

    internal List<Range> Remove(Range range)
    {
        if (!Intersects(range))
        {
            return new List<Range> { this };
        }
        else if (range.Contains(this))
        {
            return new List<Range>();
        }
        else
        {
            List<Range> res = new List<Range>(3);
            if (range.Start > Start)
            {
                res.Add(new Range(Start, range.Start - Start, Source, SecureMarks));
            }

            int end = Start + Length;
            int rangeEnd = range.Start + range.Length;
            if (rangeEnd < end)
            {
                res.Add(new Range(rangeEnd, (end - rangeEnd), Source, SecureMarks));
            }

            return res;
        }
    }

    internal Range? Intersection(Range? range)
    {
        if (range == null) { return null; }

        return Intersection(range.Value);
    }

    internal Range? Intersection(Range range)
    {
        if (Start == range.Start && Length == range.Length)
        {
            return this;
        }

        Range lead, trail;
        if (Start < range.Start)
        {
            lead = this;
            trail = range;
        }
        else
        {
            lead = range;
            trail = this;
        }

        int start = Math.Max(lead.Start, trail.Start);
        int end = Math.Min(lead.Start + lead.Length, trail.Start + trail.Length);
        if (start >= end)
        {
            return null;
        }
        else
        {
            return new Range(start, end - start);
        }
    }
}
