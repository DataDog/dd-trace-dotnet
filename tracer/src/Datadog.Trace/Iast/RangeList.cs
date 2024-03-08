// <copyright file="RangeList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#nullable enable

namespace Datadog.Trace.Iast;

internal class RangeList
{
    private readonly int _maxRangeCount = Iast.Instance.Settings.MaxRangeCount;

    private Range[] _ranges;
    private int _remaining;
    private bool _extendable;

    internal RangeList(int count)
    {
        var finalCount = count > _maxRangeCount ? _maxRangeCount : count;
        _ranges = new Range[finalCount];
        _remaining = finalCount;
        _extendable = count < _maxRangeCount;
    }

    public void Add(Range[]? ranges, int length)
    {
        if (ranges == null || length == 0)
        {
            return;
        }

        // Extend the range list if it is possible
        if (!Extend(length))
        {
            return;
        }

        // Calculate the number of ranges to add that is possible to add
        var count = Math.Min(length, _remaining);
        if (count == 0)
        {
            return;
        }

        Array.Copy(ranges, 0, _ranges, _ranges.Length - _remaining, count);
        _remaining -= count;
    }

    public void Add(Range[]? ranges)
    {
        Add(ranges, ranges?.Length ?? 0);
    }

    public bool IsFull()
    {
        return _remaining == 0 && !_extendable;
    }

    public Range[] ToArray()
    {
        return _ranges;
    }

    private bool Extend(int count)
    {
        if (!_extendable || count == 0)
        {
            return false;
        }

        var extendPossible = _maxRangeCount - _ranges.Length;
        var afterExtend = _ranges.Length + count;
        if (afterExtend > _maxRangeCount)
        {
            count = extendPossible;
        }

        var newRanges = new Range[afterExtend];
        Array.Copy(_ranges, newRanges, _ranges.Length);

        _ranges = newRanges;
        _remaining += count;
        _extendable = afterExtend < _maxRangeCount;

        return true;
    }
}
