// <copyright file="StackReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.Iast;

internal static class StackReporter
{
    public static List<Dictionary<string, object>> GetUserFrames(int maxStackTraceDepth, int topPercent, StackFrame?[] frames)
    {
        var allValidFrames = new List<Dictionary<string, object>>(frames.Length);
        uint frameId = 0;

        foreach (var frame in frames)
        {
            var method = frame?.GetMethod();
            var declaringType = method?.DeclaringType;
            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly is null || assembly.StartsWith("Datadog.Trace", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = System.IO.Path.GetFileName(frame?.GetFileName());
            var fileNameValid = !string.IsNullOrEmpty(fileName);
            var formattedFrame = new Dictionary<string, object>(7)
            {
                ["id"] = frameId
            };

            if (fileNameValid)
            {
                formattedFrame["file"] = fileName!;
                formattedFrame["line"] = (uint)frame!.GetFileLineNumber();
                formattedFrame["column"] = (uint)frame.GetFileColumnNumber();
            }

            if (declaringType?.Namespace is { Length: > 0 } @namespace)
            {
                formattedFrame["namespace"] = @namespace;
            }

            if (declaringType?.Name is { Length: > 0 } className)
            {
                formattedFrame["class_name"] = className;
            }

            if (method?.Name is { Length: > 0 } function)
            {
                formattedFrame["function"] = function;
            }

            allValidFrames.Add(formattedFrame);
            frameId++;
        }

        if (maxStackTraceDepth <= 0 || allValidFrames.Count <= maxStackTraceDepth)
        {
            return allValidFrames;
        }

        var topCount = Math.Max(1, topPercent * maxStackTraceDepth / 100);
        var bottomCount = maxStackTraceDepth - topCount;
        var trimmedFrames = new List<Dictionary<string, object>>(maxStackTraceDepth);
        trimmedFrames.AddRange(allValidFrames.GetRange(0, topCount));
        if (bottomCount > 0)
        {
            trimmedFrames.AddRange(allValidFrames.GetRange(allValidFrames.Count - bottomCount, bottomCount));
        }

        return trimmedFrames;
    }
}
