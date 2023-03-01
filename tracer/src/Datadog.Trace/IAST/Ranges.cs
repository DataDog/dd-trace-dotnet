// <copyright file="Ranges.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

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
}
