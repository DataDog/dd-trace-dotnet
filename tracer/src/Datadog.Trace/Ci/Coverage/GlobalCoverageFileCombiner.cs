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

    public static bool TryAcquireInputFiles(
        string inputFolder,
        GlobalCoverageReconciliationAuthority? authority,
        out string[] inputFiles,
        out GlobalCoverageReconciliationLease? reconciliationLease)
    {
        inputFiles = [];
        reconciliationLease = null;

        if (!GlobalCoverageReconciliation.TryAcquire(inputFolder, authority, out reconciliationLease, out _) ||
            (reconciliationLease is null && HasProtocolMarkers(inputFolder)))
        {
            return false;
        }

        // The lease certifies a closed protocol generation. Without a protocol, retain the legacy
        // behavior of combining every bounded JSON input in the directory.
        inputFiles = reconciliationLease?.SelectedInputs.Select(static input => input.Path).ToArray() ?? GetInputFilesBounded(inputFolder);
        return true;
    }

    public static bool TryCombine(
        IReadOnlyList<string> inputFiles,
        string? outputFile,
        GlobalCoverageReconciliationLease? reconciliationLease,
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

            if (!inputReader.TryRead(file, reconciliationLease?.GetCertifiedInput(file), out var globalCoverage) || globalCoverage is null)
            {
                rejectedInput = file;
                return false;
            }

            // Reporting remains caller-owned so the tracer can stay silent while the CLI preserves
            // its existing per-file progress messages.
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

    private static bool HasProtocolMarkers(string inputFolder)
        => Directory.EnumerateFiles(inputFolder, GlobalCoverageProtocol.PendingMarkerPattern, SearchOption.TopDirectoryOnly).Any() ||
           Directory.EnumerateFiles(inputFolder, GlobalCoverageProtocol.ReadyMarkerPattern, SearchOption.TopDirectoryOnly).Any() ||
           Directory.EnumerateFiles(inputFolder, GlobalCoverageProtocol.CommandOwnerClaimPattern, SearchOption.TopDirectoryOnly).Any();

    private static string[] GetInputFilesBounded(string inputFolder)
    {
        var files = Directory.EnumerateFiles(inputFolder, "*.json", SearchOption.TopDirectoryOnly)
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
