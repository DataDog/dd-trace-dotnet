// <copyright file="StackTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal static class StackTraceProcessor
{
    private static readonly string[] FramePrefixes =
    [
        "at ",
        "場所 "
    ];

    private static readonly string[] SourceLocationMarkers =
    [
        " in ",
        " 場所 "
    ];

    internal static List<string> ParseFrames(string exceptionString)
    {
        if (string.IsNullOrEmpty(exceptionString))
        {
            throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
        }

        var results = new List<string>();
        var currentSpan = exceptionString.AsSpan();

        while (!currentSpan.IsEmpty)
        {
            var lineEndIndex = currentSpan.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> line;

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
    private static void ProcessLine(ReadOnlySpan<char> line, List<string> results)
    {
        line = line.TrimStart();
        if (line.IsEmpty)
        {
            return;
        }

        // Check if it's a stack frame line (starts with "at ")
        if (!TryStripFramePrefix(line, out line))
        {
            return;
        }

        // Find the " in " marker and truncate if found
        var inIndex = FindSourceLocationIndex(line);

        if (inIndex > 0)
        {
            line = line.Slice(0, inIndex);
        }

        results.Add(line.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryStripFramePrefix(ReadOnlySpan<char> line, out ReadOnlySpan<char> frameLine)
    {
        for (var i = 0; i < FramePrefixes.Length; i++)
        {
            var prefix = FramePrefixes[i].AsSpan();
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                frameLine = line.Slice(prefix.Length);
                return true;
            }
        }

        frameLine = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSourceLocationIndex(ReadOnlySpan<char> line)
    {
        for (var i = 0; i < SourceLocationMarkers.Length; i++)
        {
            var index = line.IndexOf(SourceLocationMarkers[i].AsSpan(), StringComparison.Ordinal);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }
}
