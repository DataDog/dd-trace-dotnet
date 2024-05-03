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

    public static Dictionary<string, object>? GetStack(int maxStackTraceDepth, string id)
    {
        var frames = GetFrames(maxStackTraceDepth);

        if (frames is null || frames.Count == 0)
        {
            return null;
        }

        return MetaStructHelper.StackTraceInfoToDictionary(null, _language, id, null, frames);
    }

    private static List<Dictionary<string, object>> GetFrames(int maxStackTraceDepth)
    {
        var stackTrace = new StackTrace(true);
        var stackFrameList = new List<Dictionary<string, object>>(maxStackTraceDepth);
        int counter = 0;

        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame?.GetMethod()?.DeclaringType;
            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !AssemblyExcluded(assembly))
            {
                var fileName = System.IO.Path.GetFileName(frame?.GetFileName());
                var fileNameValid = !string.IsNullOrEmpty(fileName);
                stackFrameList.Add(MetaStructHelper.StackFrameToDictionary(
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

            if (maxStackTraceDepth > 0 && counter >= maxStackTraceDepth)
            {
                break;
            }
        }

        return stackFrameList;
    }

    private static bool AssemblyExcluded(string assembly)
    {
        return assembly.StartsWith("Datadog.Trace", StringComparison.OrdinalIgnoreCase);
    }
}
