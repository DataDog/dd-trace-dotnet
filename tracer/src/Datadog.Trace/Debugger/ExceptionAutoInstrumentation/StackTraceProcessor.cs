// <copyright file="StackTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.VendoredMicrosoftCode.System;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal static class StackTraceProcessor
{
    internal static List<string> ParseFrames(string exceptionString)
    {
        if (string.IsNullOrEmpty(exceptionString))
        {
            throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
        }

        var results = new List<string>();
        var currentSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(exceptionString);

        while (!currentSpan.IsEmpty)
        {
            var lineEndIndex = currentSpan.IndexOfAny('\r', '\n');
            VendoredMicrosoftCode.System.ReadOnlySpan<char> line;

            if (lineEndIndex >= 0)
            {
                line = currentSpan.Slice(0, lineEndIndex);
                currentSpan = currentSpan.Slice(lineEndIndex + 1);
                if (!currentSpan.IsEmpty && currentSpan[0] == '\n')
                {
                    currentSpan = currentSpan.Slice(1);
                }
            }
            else
            {
                line = currentSpan;
                currentSpan = default;
            }

            ProcessLine(line, results);
        }

        return results;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessLine(VendoredMicrosoftCode.System.ReadOnlySpan<char> line, List<string> results)
    {
        line = line.TrimStart();
        if (line.IsEmpty)
        {
            return;
        }

        // Check if it's a stack frame line (starts with "at ")
        if (!VendoredMicrosoftCode.System.MemoryExtensions.StartsWith(line, VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at "), StringComparison.Ordinal))
        {
            return;
        }

        // Skip the "at " prefix
        line = line.Slice(3);

        // Find the " in " marker and truncate if found
        var inIndex = VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(line, VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(" in "), StringComparison.Ordinal);

        if (inIndex > 0)
        {
            line = line.Slice(0, inIndex);
        }

        results.Add(line.ToString());
    }
}
