// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Datadog.Trace;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

internal static class CoverageUtils
{
    private const int MaximumInputFiles = 65_536;

    public static bool TryCombineAndGetTotalCoverage(string inputFolder, string outputFile, bool useStdOut)
    {
        return TryCombineAndGetTotalCoverage(inputFolder, outputFile, out _, useStdOut);
    }

    public static bool TryCombineAndGetTotalCoverage(string inputFolder, string outputFile, out GlobalCoverageInfo globalCoverageInfo, bool useStdOut)
    {
        if (string.IsNullOrEmpty(outputFile))
        {
            if (useStdOut)
            {
                Utils.WriteError("<output-file> is empty.");
            }

            globalCoverageInfo = null;
            return false;
        }

        GlobalCoverageReconciliationLease reconciliationLease = null;
        try
        {
            if (!TryLoadAndCombine(inputFolder, outputFile, out globalCoverageInfo, out reconciliationLease, useStdOut))
            {
                return false;
            }

            if (useStdOut)
            {
                Utils.WriteSuccess($"Writing {outputFile}");
            }

            new GlobalCoverageArtifactWriter().WriteAtomicReplace(outputFile, globalCoverageInfo);
            reconciliationLease?.Complete();
            return true;
        }
        finally
        {
            reconciliationLease?.Dispose();
        }
    }

    private static bool TryLoadAndCombine(
        string inputFolder,
        string outputFile,
        out GlobalCoverageInfo globalCoverageInfo,
        out GlobalCoverageReconciliationLease reconciliationLease,
        bool useStdOut = true)
    {
        globalCoverageInfo = default;
        reconciliationLease = null;

        if (string.IsNullOrEmpty(inputFolder))
        {
            if (useStdOut)
            {
                Utils.WriteError("<input-folder> is empty.");
            }

            return false;
        }

        if (!Directory.Exists(inputFolder))
        {
            if (useStdOut)
            {
                Utils.WriteError($"'{inputFolder}' doesn't exist.");
            }

            return false;
        }

        var jsonFiles = Array.Empty<string>();
        try
        {
            if (!GlobalCoverageReconciliation.TryAcquire(inputFolder, authority: null, out reconciliationLease, out _) ||
                (reconciliationLease is null && HasProtocolMarkers(inputFolder)))
            {
                return false;
            }

            jsonFiles = reconciliationLease?.SelectedInputs.Select(static input => input.Path).ToArray() ?? GetInputFilesBounded(inputFolder);
            if (jsonFiles.Length == 0)
            {
                reconciliationLease?.Complete();
                if (useStdOut)
                {
                    Utils.WriteError($"'{inputFolder}' doesn't contain any json file.");
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Utils.WriteError("Error reading json files.");
            AnsiConsole.WriteException(ex);
        }

        var inputReader = new GlobalCoverageInputReader();
        var accumulator = new GlobalCoverageCombinerAccumulator();
        var processedFiles = 0;
        var outputFullPath = string.IsNullOrWhiteSpace(outputFile) ? null : Path.GetFullPath(outputFile);
        foreach (var file in jsonFiles)
        {
            if (Path.GetFileName(file).StartsWith("session-coverage-", StringComparison.OrdinalIgnoreCase) ||
                (outputFullPath is not null && PathsEqual(Path.GetFullPath(file), outputFullPath)))
            {
                continue;
            }

            if (!inputReader.TryRead(file, reconciliationLease?.GetCertifiedInput(file), out var globalCoverage) || globalCoverage is null)
            {
                if (useStdOut)
                {
                    Utils.WriteError($"Error processing {file}");
                }

                return false;
            }

            if (useStdOut)
            {
                Utils.WriteSuccess($"Processing: {file}");
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
