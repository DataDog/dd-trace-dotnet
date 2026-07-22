// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

internal static class CoverageUtils
{
    private const int MaximumInputFiles = 65_536;
    internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

    public static bool TryCombineAndGetTotalCoverage(string inputFolder, string outputFile)
    {
        return TryCombineAndGetTotalCoverage(inputFolder, outputFile, out _);
    }

    public static bool TryCombineAndGetTotalCoverage(string? inputFolder, string? outputFile, out GlobalCoverageInfo? globalCoverageInfo)
    {
        globalCoverageInfo = null;
        if (string.IsNullOrEmpty(outputFile))
        {
            globalCoverageInfo = null;
            return false;
        }

        GlobalCoverageReconciliationLease? reconciliationLease = null;
        try
        {
            if (!TryReadAndCombine(inputFolder, outputFile, authority: null, out globalCoverageInfo, out reconciliationLease))
            {
                return false;
            }

            new GlobalCoverageArtifactWriter().WriteAtomicReplace(outputFile!, globalCoverageInfo!);
            reconciliationLease?.Complete();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing output file: {File}", outputFile);
        }
        finally
        {
            reconciliationLease?.Dispose();
        }

        return false;
    }

    internal static bool TryReadAndCombine(
        string? inputFolder,
        string? outputFile,
        GlobalCoverageReconciliationAuthority? authority,
        out GlobalCoverageInfo? globalCoverageInfo,
        out GlobalCoverageReconciliationLease? reconciliationLease)
    {
        globalCoverageInfo = default;
        reconciliationLease = null;

        try
        {
            if (string.IsNullOrEmpty(inputFolder))
            {
                return false;
            }

            if (!Directory.Exists(inputFolder))
            {
                Log.Error("'{InputFolder}' doesn't exist.", inputFolder);
                return false;
            }

            if (!GlobalCoverageReconciliation.TryAcquire(inputFolder!, authority, out reconciliationLease, out _) ||
                (reconciliationLease is null && HasProtocolMarkers(inputFolder!)))
            {
                return false;
            }

            var jsonFiles = reconciliationLease?.SelectedInputs.Select(static input => input.Path).ToArray() ?? GetInputFilesBounded(inputFolder!);
            if (jsonFiles.Length == 0)
            {
                reconciliationLease?.Complete();
                Log.ErrorSkipTelemetry("'{InputFolder}' doesn't contain any json file.", inputFolder);
                return false;
            }

            var inputReader = new GlobalCoverageInputReader();
            var accumulator = new GlobalCoverageCombinerAccumulator();
            var processedFiles = 0;
            var outputFullPath = StringUtil.IsNullOrWhiteSpace(outputFile) ? null : Path.GetFullPath(outputFile);
            foreach (var file in jsonFiles)
            {
                if (Path.GetFileName(file).StartsWith("session-coverage-", StringComparison.OrdinalIgnoreCase) ||
                    (outputFullPath is not null && PathsEqual(Path.GetFullPath(file), outputFullPath)))
                {
                    continue;
                }

                if (!inputReader.TryRead(file, reconciliationLease?.GetCertifiedInput(file), out var globalCoverage) || globalCoverage is null)
                {
                    Log.Error("Error processing global coverage input: {File}", file);
                    return false;
                }

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
        catch (Exception globalEx)
        {
            Log.Error(globalEx, "Error combining all code coverages for the folder: {Folder}", inputFolder);
        }

        return false;
    }

    private static bool HasProtocolMarkers(string inputFolder)
        => Directory.EnumerateFiles(inputFolder, ".dd-coverage-process-incomplete-*", SearchOption.TopDirectoryOnly).Any() ||
           Directory.EnumerateFiles(inputFolder, ".dd-coverage-process-ready-*", SearchOption.TopDirectoryOnly).Any() ||
           Directory.EnumerateFiles(inputFolder, ".dd-coverage-command-owner-*.claim", SearchOption.TopDirectoryOnly).Any();

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
