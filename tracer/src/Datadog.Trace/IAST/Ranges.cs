// <copyright file="Ranges.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Iast;
internal static class Ranges
{
    public static void CopyShift(Range[] src, Range[] dst, int dstPos, int shift)
    {
        if (shift == 0)
        {
            Array.Copy(src, 0, dst, dstPos, src.Length);
        }
        else
        {
            for (int iSrc = 0, iDst = dstPos; iSrc < src.Length; iSrc++, iDst++)
            {
                dst[iDst] = src[iSrc].Shift(shift);
            }
        }
    }

    public static Range[] MergeRanges(int offset, Range[] rangesLeft, Range[] rangesRight)
    {
        int nRanges = rangesLeft.Length + rangesRight.Length;
        Range[] ranges = new Range[nRanges];
        if (rangesLeft.Length > 0)
        {
            Array.Copy(rangesLeft, 0, ranges, 0, rangesLeft.Length);
        }

        if (rangesRight.Length > 0)
        {
            Ranges.CopyShift(rangesRight, ranges, rangesLeft.Length, offset);
        }

        return ranges;
    }

    public static void GetIncludedRangesInterval(int offset, int length, Range[] ranges, out int start, out int end)
    {
        // index of the first included range
        start = -1;
        // index of the first not included range
        end = -1;
        for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
        {
            var rangeSelf = ranges[rangeIndex];
            if (rangeSelf.Start < offset + length && rangeSelf.Start + rangeSelf.Length > offset)
            {
                if (start == -1)
                {
                    start = rangeIndex;
                }
            }
            else if (start != -1)
            {
                end = rangeIndex;
                break;
            }
        }
    }

    public static Range[]? ForSubstring(int offset, int length, Range[] ranges)
    {
        GetIncludedRangesInterval(offset, length, ranges, out int firstRangeIncludedIndex, out int lastRangeExcludedIndex);

        // No ranges in the interval
        if (firstRangeIncludedIndex == -1)
        {
            return null;
        }

        if (lastRangeExcludedIndex == -1)
        {
            lastRangeExcludedIndex = ranges.Length;
        }

        var newRangesSize = lastRangeExcludedIndex - firstRangeIncludedIndex;
        var newRanges = new Range[newRangesSize];
        for (int rangeIndex = firstRangeIncludedIndex, newRangeIndex = 0; newRangeIndex < newRangesSize; rangeIndex++, newRangeIndex++)
        {
            Range range = ranges[rangeIndex];
            if (offset == 0 && range.Start + range.Length <= length)
            {
                newRanges[newRangeIndex] = range;
            }
            else
            {
                var newStart = range.Start - offset;
                var newLength = range.Length;
                var newEnd = newStart + newLength;
                if (newStart < 0)
                {
                    newLength = newLength + newStart;
                    newStart = 0;
                }

                if (newEnd > length)
                {
                    newLength = length - newStart;
                }

                if (newLength > 0)
                {
                    newRanges[newRangeIndex] = new Range(newStart, newLength, range.Source);
                }
            }
        }

        return newRanges;
    }

    internal static Range[] ForRemove(int beginIndex, int endIndex, Range[] ranges)
    {
        var newRanges = new List<Range>();

        for (var i = 0; i < ranges.Length; i++)
        {
            var startBeforeRemoveArea = ranges[i].Start < beginIndex;
            var endAfterRemoveArea = ranges[i].Start + ranges[i].Length > endIndex;
            var starAfterRemoveArea = ranges[i].Start > endIndex;
            int newStart, newEnd;

            // range is inside the removed area
            if (!startBeforeRemoveArea && !starAfterRemoveArea && !endAfterRemoveArea)
            {
                continue;
            }

            if (startBeforeRemoveArea)
            {
                newStart = ranges[i].Start;
            }
            else
            {
                newStart = starAfterRemoveArea ? ranges[i].Start - (endIndex - beginIndex) : beginIndex;
            }

            // endBeforeRemoveArea
            if (ranges[i].Start + ranges[i].Length < beginIndex)
            {
                newEnd = ranges[i].Start + ranges[i].Length;
            }
            else
            {
                newEnd = endAfterRemoveArea ? ranges[i].Start + ranges[i].Length - (endIndex - beginIndex) : beginIndex;
            }

            var newRange = new Range(newStart, newEnd - newStart, ranges[i].Source);
            newRanges.Add(newRange);
        }

        return newRanges.ToArray();
    }
}
