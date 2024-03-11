// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal static class SpanExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanCharSplitter Split(this ReadOnlySpan<char> source, string separator, int count = int.MaxValue)
    {
        return new SpanCharSplitter(source, separator.AsSpan(), count);
    }
}
