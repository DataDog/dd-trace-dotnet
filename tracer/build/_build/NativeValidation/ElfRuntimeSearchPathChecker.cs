// <copyright file="ElfRuntimeSearchPathChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace NativeValidation;

internal static class ElfRuntimeSearchPathChecker
{
    public static void Validate(
        IEnumerable<string> readElfDynamicSection,
        string expectedSearchPath,
        string filename)
    {
        var searchPaths = new List<string>();

        foreach (var line in readElfDynamicSection)
        {
            if (line.IndexOf("(RPATH)", StringComparison.Ordinal) < 0
             && line.IndexOf("(RUNPATH)", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var valueStart = line.LastIndexOf('[');
            var valueEnd = line.LastIndexOf(']');
            if (valueStart < 0 || valueEnd <= valueStart)
            {
                throw new InvalidOperationException(
                    $"Unable to parse the RPATH/RUNPATH entry for {filename}: {line.Trim()}");
            }

            searchPaths.Add(line.Substring(valueStart + 1, valueEnd - valueStart - 1));
        }

        if (searchPaths.Count == 1
         && string.Equals(searchPaths[0], expectedSearchPath, StringComparison.Ordinal))
        {
            return;
        }

        var received = searchPaths.Count == 0
                           ? "<none>"
                           : string.Join(", ", searchPaths);

        throw new InvalidOperationException(
            $"{filename} must have exactly one RPATH/RUNPATH entry equal to '{expectedSearchPath}', but found: {received}");
    }
}
