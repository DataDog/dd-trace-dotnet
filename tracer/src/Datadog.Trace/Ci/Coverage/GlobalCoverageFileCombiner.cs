// <copyright file="GlobalCoverageFileCombiner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal static class GlobalCoverageFileCombiner
{
    private const int MaximumInputFiles = 65_536;

    public static bool TryAcquireInputFiles(string inputFolder, string? expectedRunToken, out string[] inputFiles)
    {
        inputFiles = [];
        var pendingPattern = expectedRunToken is null
                                 ? GlobalCoverageProtocol.PendingMarkerPattern
                                 : GlobalCoverageProtocol.PendingMarkerPrefix + expectedRunToken + "-*";
        if (Directory.EnumerateFiles(inputFolder, pendingPattern, SearchOption.TopDirectoryOnly).Any())
        {
            return false;
        }

        var inputPattern = expectedRunToken is null
                               ? "*.json"
                               : GlobalCoverageProtocol.CoverageFilePrefix + expectedRunToken + "-*" + GlobalCoverageProtocol.JsonExtension;
        inputFiles = GetInputFilesBounded(inputFolder, inputPattern);
        return true;
    }

    public static bool TryCombine(
        IReadOnlyList<string> inputFiles,
        string? outputFile,
        bool requireAllInputs,
        Action<string>? onFileProcessed,
        out GlobalCoverageInfo? globalCoverageInfo,
        out string? rejectedInput)
    {
        globalCoverageInfo = null;
        rejectedInput = null;

        var inputReader = new GlobalCoverageInputReader();
        var accumulator = new GlobalCoverageCombinerAccumulator();
        var processedFiles = 0;
        var outputFullPath = StringUtil.IsNullOrWhiteSpace(outputFile) ? null : Path.GetFullPath(outputFile);
        foreach (var file in inputFiles)
        {
            if (Path.GetFileName(file).StartsWith("session-coverage-", StringComparison.OrdinalIgnoreCase) ||
                (outputFullPath is not null && PathsEqual(Path.GetFullPath(file), outputFullPath)))
            {
                continue;
            }

            if (!inputReader.TryRead(file, out var globalCoverage) || globalCoverage is null)
            {
                // Legacy directories may contain unrelated JSON. Run-scoped callers require every
                // selected process artifact to be valid so incomplete coverage cannot be published.
                if (requireAllInputs)
                {
                    rejectedInput = file;
                    return false;
                }

                continue;
            }

            onFileProcessed?.Invoke(file);
            accumulator.Add(globalCoverage);
            processedFiles++;
        }

        if (processedFiles == 0)
        {
            return false;
        }

        globalCoverageInfo = accumulator.Materialize();
        return true;
    }

    private static string[] GetInputFilesBounded(string inputFolder, string pattern)
    {
        var files = Directory.EnumerateFiles(inputFolder, pattern, SearchOption.TopDirectoryOnly)
                             .Take(MaximumInputFiles + 1)
                             .ToArray();
        if (files.Length > MaximumInputFiles)
        {
            throw new InvalidDataException("The global coverage input-file limit was exceeded.");
        }

        return files;
    }

    private static bool PathsEqual(string first, string second)
        => string.Equals(
            first,
            second,
            FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
