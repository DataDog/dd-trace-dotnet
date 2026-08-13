// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

internal static class CoverageUtils
{
    internal static readonly IDatadogLogger Log = TestOptimization.Instance.Log;

    public static bool TryCombineAndGetTotalCoverage(string inputFolder, string outputFile)
    {
        return TryCombineAndGetTotalCoverage(inputFolder, outputFile, out _);
    }

    public static bool TryCombineAndGetTotalCoverage(string? inputFolder, string? outputFile, out GlobalCoverageInfo? globalCoverageInfo)
    {
        globalCoverageInfo = null;
        if (StringUtil.IsNullOrEmpty(outputFile))
        {
            globalCoverageInfo = null;
            return false;
        }

        try
        {
            if (!TryReadAndCombine(inputFolder, outputFile, expectedRunToken: null, out globalCoverageInfo))
            {
                return false;
            }

            var writer = new GlobalCoverageArtifactWriter();
            using var stagedOutput = writer.StageReplace(outputFile!, globalCoverageInfo!);
            stagedOutput.Commit();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing output file: {File}", outputFile);
        }

        return false;
    }

    public static bool TryReadAndCombine(
        string? inputFolder,
        string? outputFile,
        string? expectedRunToken,
        out GlobalCoverageInfo? globalCoverageInfo)
    {
        globalCoverageInfo = default;

        try
        {
            if (StringUtil.IsNullOrEmpty(inputFolder))
            {
                return false;
            }

            if (!Directory.Exists(inputFolder))
            {
                Log.Error("'{InputFolder}' doesn't exist.", inputFolder);
                return false;
            }

            if (!GlobalCoverageFileCombiner.TryAcquireInputFiles(inputFolder!, expectedRunToken, out var jsonFiles))
            {
                return false;
            }

            if (jsonFiles.Length == 0)
            {
                Log.ErrorSkipTelemetry("'{InputFolder}' doesn't contain any json file.", inputFolder);
                return false;
            }

            if (!GlobalCoverageFileCombiner.TryCombine(
                    jsonFiles,
                    outputFile,
                    requireAllInputs: expectedRunToken is not null,
                    onFileProcessed: null,
                    out globalCoverageInfo,
                    out var rejectedInput))
            {
                if (rejectedInput is not null)
                {
                    Log.Error("Error processing global coverage input: {File}", rejectedInput);
                }

                return false;
            }

            return true;
        }
        catch (Exception globalEx)
        {
            Log.Error(globalEx, "Error combining all code coverages for the folder: {Folder}", inputFolder);
        }

        return false;
    }
}
