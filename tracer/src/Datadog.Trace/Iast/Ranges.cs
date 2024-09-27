// <copyright file="Ranges.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Iast;

internal static class Ranges
{
    public static void CopyShift(Range[] src, Range[] dst, int dstPos, int shift)
    {
        CopyShift(src, dst, dstPos, shift, src.Length);
    }

    public static void CopyShift(Range[] src, Range[] dst, int dstPos, int shift, int max)
    {
        var srcLength = Math.Min(src.Length, max);

        if (shift == 0)
        {
            Array.Copy(src, 0, dst, dstPos, srcLength);
        }
        else
        {
            for (int iSrc = 0, iDst = dstPos; iSrc < srcLength; iSrc++, iDst++)
            {
                dst[iDst] = src[iSrc].Shift(shift);
            }
        }
    }

    public static Range[] MergeRanges(int offset, Range[] rangesLeft, Range[] rangesRight)
    {
        var nRanges = rangesLeft.Length + rangesRight.Length;
        var finalRangesCount = nRanges > Iast.Instance.Settings.MaxRangeCount ? Iast.Instance.Settings.MaxRangeCount : nRanges;

        // Don't allocate a new array if the left ranges count is the same as the maximum number of ranges allowed
        // No more ranges can be added to that array
        if (rangesLeft.Length == Iast.Instance.Settings.MaxRangeCount)
        {
            return rangesLeft;
        }

        var ranges = new Range[finalRangesCount];
        var remainingRanges = ranges.Length;

        if (rangesLeft.Length > 0)
        {
            var count = Math.Min(rangesLeft.Length, remainingRanges);
            Array.Copy(rangesLeft, 0, ranges, 0, count);
            remainingRanges -= count;
        }

        if (rangesRight.Length > 0 && remainingRanges > 0)
        {
            CopyShift(rangesRight, ranges, rangesLeft.Length, offset, remainingRanges);
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
        if (newRangesSize > Iast.Instance.Settings.MaxRangeCount)
        {
            // Truncate the ranges to the maximum number of ranges
            newRangesSize = Iast.Instance.Settings.MaxRangeCount;
        }

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
                    newRanges[newRangeIndex] = new Range(newStart, newLength, range.Source, range.SecureMarks);
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

            // Check if the range array is already full to not exceed the maximum number of ranges
            if (newRanges.Count > Iast.Instance.Settings.MaxRangeCount)
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

            var newRange = new Range(newStart, newEnd - newStart, ranges[i].Source, ranges[i].SecureMarks);
            newRanges.Add(newRange);
        }

        return newRanges.ToArray();
    }

    internal static Range[] CopyWithMark(Range[] ranges, SecureMarks secureMarks)
    {
        var newRanges = new List<Range>();

        foreach (var range in ranges)
        {
            var newRange = new Range(range.Start, range.Length, range.Source, range.SecureMarks | secureMarks);
            newRanges.Add(newRange);
        }

        return newRanges.ToArray();
    }

    internal static Range[] GetUnsafeRanges(Range[] ranges, SecureMarks safeMarks, SourceType[]? safeSources)
    {
        if (safeMarks == SecureMarks.None && safeSources is null)
        {
            return ranges;
        }

        int insecureCount = 0;
        for (int x = 0; x < ranges.Length; x++)
        {
            var range = ranges[x];
            if (!range.IsSecure(safeMarks, safeSources))
            {
                insecureCount++;
            }
        }

        if (insecureCount == ranges.Length)
        {
            // This is made in order to avoid unnecessary allocations (most common situation)
            return ranges;
        }
        else if (insecureCount == 0)
        {
            return [];
        }

        Range[] insecureRanges = new Range[insecureCount];
        int i = 0;
        for (int x = 0; x < ranges.Length; x++)
        {
            var range = ranges[x];
            if (!range.IsSecure(safeMarks, safeSources))
            {
                insecureRanges[i] = range;
                i++;
            }
        }

        return insecureRanges;
    }

    /// <summary>
    /// Returns an array of ranges without ranges that are not marked with the given marks.
    /// </summary>
    internal static Range[]? UnsafeRanges(Range[]? ranges, SecureMarks secureMarks)
    {
        if (ranges is null || secureMarks == SecureMarks.None)
        {
            return ranges;
        }

        var newRanges = new Range[ranges.Length];

        var length = 0;
        for (var i = 0; i < ranges.Length; i++)
        {
            // Keep the vulnerable range if not marked with the given marks
            if (!ranges[i].IsMarked(secureMarks))
            {
                newRanges[length++] = ranges[i];
            }
        }

        if (length == 0)
        {
            return null;
        }

#if NETCOREAPP
        return new ArraySegment<Range>(newRanges, 0, length).ToArray();
#else
        var result = new Range[length];
        Array.Copy(newRanges, 0, result, 0, length);
        return result;
#endif
    }
}
