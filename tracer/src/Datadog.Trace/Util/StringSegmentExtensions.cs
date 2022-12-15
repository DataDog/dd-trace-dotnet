// <copyright file="StringSegmentExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

namespace Datadog.Trace.Util;

internal static class StringSegmentExtensions
{
    public static StringBuilder Append(this StringBuilder sb, StringSegment value)
    {
        value.AppendTo(sb);
        return sb;
    }

    public static StringSegment Slice(this string? value)
    {
        return new StringSegment(value);
    }

    public static StringSegment Slice(this string? value, int start)
    {
        return new StringSegment(value, start);
    }

    public static StringSegment Slice(this string? value, int start, int length)
    {
        return new StringSegment(value, start, length);
    }
}
