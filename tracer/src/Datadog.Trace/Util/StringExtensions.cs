// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanCharSplitter SplitIntoSpans(this string source, char separator, int count = int.MaxValue)
    {
        return new SpanCharSplitter(source, separator, count);
    }
}
