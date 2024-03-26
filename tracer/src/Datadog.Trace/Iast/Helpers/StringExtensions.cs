// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Iast.Helpers;

internal static class StringExtensions
{
    public static unsafe string? CreateNewReference(this string? text)
    {
        if (text is null) { return null; }
        if (text.Length == 0) { return string.Empty; }
#if NET6_0_OR_GREATER
        return new string(text.AsSpan());
#else
        fixed (char* c = text)
        {
            return new string(c, 0, text.Length);
        }
#endif
    }
}
