// <copyright file="ElfDynamicLinkingChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeValidation;

internal static class ElfDynamicLinkingChecker
{
    private const string NeededMarker = "(NEEDED)";
    private const string InterpreterMarker = "[Requesting program interpreter:";

    public static void ValidateNeededLibraries(
        IEnumerable<string> readElfDynamicSection,
        IReadOnlyCollection<string> allowedLibraries,
        string filename)
    {
        var neededLibraries = new List<string>();

        foreach (var line in readElfDynamicSection)
        {
            if (line.IndexOf(NeededMarker, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var valueStart = line.LastIndexOf('[');
            var valueEnd = line.LastIndexOf(']');
            if (valueStart < 0 || valueEnd <= valueStart)
            {
                throw new InvalidOperationException(
                    $"Unable to parse the NEEDED entry for {filename}: {line.Trim()}");
            }

            neededLibraries.Add(line.Substring(valueStart + 1, valueEnd - valueStart - 1));
        }

        if (neededLibraries.Count == 0)
        {
            throw new InvalidOperationException(
                $"{filename} has no NEEDED entries. Validate the uncompressed, dynamically linked binary.");
        }

        var unexpectedLibraries = neededLibraries
                                 .Where(x => !allowedLibraries.Contains(x))
                                 .Distinct()
                                 .OrderBy(x => x, StringComparer.Ordinal)
                                 .ToList();

        if (unexpectedLibraries.Count > 0)
        {
            throw new InvalidOperationException(
                $"{filename} depends on libraries outside the allowed set ({string.Join(", ", allowedLibraries)}):{Environment.NewLine}"
              + string.Join(Environment.NewLine, unexpectedLibraries.Select(x => $"  {x}")));
        }
    }

    public static void ValidateInterpreter(
        IEnumerable<string> readElfProgramHeaders,
        string expectedInterpreter,
        string filename)
    {
        var interpreters = new List<string>();

        foreach (var line in readElfProgramHeaders)
        {
            var markerStart = line.IndexOf(InterpreterMarker, StringComparison.Ordinal);
            if (markerStart < 0)
            {
                continue;
            }

            var valueStart = markerStart + InterpreterMarker.Length;
            var valueEnd = line.IndexOf(']', valueStart);
            if (valueEnd < 0)
            {
                throw new InvalidOperationException(
                    $"Unable to parse the program interpreter for {filename}: {line.Trim()}");
            }

            interpreters.Add(line.Substring(valueStart, valueEnd - valueStart).Trim());
        }

        if (interpreters.Count == 1
         && string.Equals(interpreters[0], expectedInterpreter, StringComparison.Ordinal))
        {
            return;
        }

        var received = interpreters.Count == 0
                           ? "<none>"
                           : string.Join(", ", interpreters);

        throw new InvalidOperationException(
            $"{filename} must request the program interpreter '{expectedInterpreter}', but found: {received}");
    }
}
