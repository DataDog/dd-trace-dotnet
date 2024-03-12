// <copyright file="RangeList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal class RangeList
{
    private readonly int _maxRangeCount = Iast.Instance.Settings.MaxRangeCount;

    private readonly Range[] _ranges;
    private int _remaining;

    internal RangeList(int count)
    {
        var finalCount = Math.Min(count, _maxRangeCount);
        _ranges = new Range[finalCount];
        _remaining = finalCount;
    }

    public void Add(Range[]? ranges, int length, int shift)
    {
        if (ranges == null || length == 0 || _remaining == 0)
        {
            return;
        }

        // Calculate the number of ranges to add that is possible to add
        var count = Math.Min(length, _remaining);

        Ranges.CopyShift(ranges, _ranges, _ranges.Length - _remaining, shift, count);
        _remaining -= count;
    }

    public void Add(Range[]? ranges, int shift)
    {
        Add(ranges, ranges?.Length ?? 0, shift);
    }

    public Range[] ToArray()
    {
        return _ranges;
    }
}
