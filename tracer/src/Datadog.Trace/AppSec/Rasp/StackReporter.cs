// <copyright file="StackReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace Datadog.Trace.AppSec.Rasp;

internal static class StackReporter
{
    private const string _language = "dotnet";

    public static Dictionary<string, object>? GetStack(int maxStackTraceDepth, string id, StackFrame[]? stackFrames = null)
    {
        var frames = GetFrames(maxStackTraceDepth, stackFrames ?? new StackTrace(true).GetFrames());

        if (frames is null || frames.Count == 0)
        {
            return null;
        }

        return MetaStructHelper.StackTraceInfoToDictionary(null, _language, id, null, frames);
    }

    private static List<Dictionary<string, object>> GetFrames(int maxStackTraceDepth, StackFrame?[] frames)
    {
        var allValidFrames = new List<Dictionary<string, object>>(frames.Length);
        int counter = 0;

        // Collect all valid frames
        foreach (var frame in frames)
        {
            var declaringType = frame?.GetMethod()?.DeclaringType;
            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !AssemblyExcluded(assembly))
            {
                var fileName = System.IO.Path.GetFileName(frame?.GetFileName());
                var fileNameValid = !string.IsNullOrEmpty(fileName);
                allValidFrames.Add(MetaStructHelper.StackFrameToDictionary(
                    (uint)counter,
                    null,
                    fileName,
                    (uint?)(fileNameValid ? frame?.GetFileLineNumber() : null),
                    (uint?)(fileNameValid ? frame?.GetFileColumnNumber() : null),
                    declaringType?.Namespace,
                    declaringType?.Name,
                    frame?.GetMethod()?.Name));
                counter++;
            }
        }

        // Determine if we need to trim the stack
        if (maxStackTraceDepth > 0 && allValidFrames.Count > maxStackTraceDepth)
        {
            int topCount = Math.Max(1, (int)(0.25 * maxStackTraceDepth));
            int bottomCount = maxStackTraceDepth - topCount;
            var trimmedStackFrames = new List<Dictionary<string, object>>(maxStackTraceDepth);
            // Add the top 25% frames
            trimmedStackFrames.AddRange(allValidFrames.GetRange(0, topCount));
            // Add the bottom 75% frames
            trimmedStackFrames.AddRange(allValidFrames.GetRange(allValidFrames.Count - bottomCount, bottomCount));

            return trimmedStackFrames;
        }

        return allValidFrames;
    }

    private static bool AssemblyExcluded(string assembly)
    {
        return assembly.StartsWith("Datadog.Trace", StringComparison.OrdinalIgnoreCase);
    }
}
