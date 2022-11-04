// <copyright file="Ranges.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;
internal class Ranges
{
    // public static Range[] EMPTY = new Range[0];

    public static Range[] ForString(string obj, Source? source)
    {
        return new Range[] { new Range(0, obj.Length, source) };
    }

    /*
    public static void copyShift(Range[] src, Range[] dst, final dstPos, int shift)
    {
        for (int iSrc = 0, iDst = dstPos; iSrc < src.Length; iSrc++, iDst++)
        {
            dst[iDst] = src[iSrc].Shift(shift);
        }
    }
    */
}
