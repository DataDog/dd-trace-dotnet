// <copyright file="StackReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

#nullable enable

namespace Datadog.Trace.AppSec.Rasp;

internal static class StackReporter
{
    private const string _language = "dotnet";

    public static StackTraceInfo? GetStack(SecuritySettings settings)
    {
        var frames = GetFrames(settings);

        if (frames is null || frames.Count == 0)
        {
            return null;
        }

        var id = Guid.NewGuid().ToString();
        var stack = new StackTraceInfo(null, _language, id, null, frames);

        return stack;
    }

    private static List<StackFrame>? GetFrames(SecuritySettings settings)
    {
        var stackTrace = new System.Diagnostics.StackTrace(true);
        var stackFrameList = new List<StackFrame>(stackTrace.FrameCount);
        int counter = 0;

        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame?.GetMethod()?.DeclaringType;
            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !AssemblyExcluded(assembly))
            {
                stackFrameList.Add(new StackFrame((uint)counter, null, System.IO.Path.GetFileName(frame?.GetFileName()), (uint?)frame?.GetFileLineNumber(), (uint?)frame?.GetFileColumnNumber(), declaringType?.Namespace, declaringType?.Name, frame?.GetMethod()?.Name));
                counter++;
            }

            if (settings.MaxStackTraceDepth > 0 && counter >= settings.MaxStackTraceDepth)
            {
                break;
            }
        }

        return stackFrameList;
    }

    private static bool AssemblyExcluded(string assembly)
    {
        return assembly.Equals("Datadog.Trace", StringComparison.OrdinalIgnoreCase);
    }
}
