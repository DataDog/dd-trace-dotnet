// <copyright file="StringBuilderExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;

#if !NETCOREAPP3_1_OR_GREATER

namespace Datadog.Trace.ExtensionMethods;

internal static class StringBuilderExtensions
{
    // .NET Core 3.1 and later has this StringBuilder.Append(ReadOnlySpan<char>) method overload built-in
    public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            sb.Append(c);
        }

        return sb;
    }
}

#endif
