// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

internal static class CoverageUtils
{
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

        if (!TryLoadAndCombine(inputFolder, outputFile, out globalCoverageInfo, useStdOut))
        {
            return false;
        }

        if (useStdOut)
        {
            Utils.WriteSuccess($"Writing {outputFile}");
        }

        var writer = new GlobalCoverageArtifactWriter();
        using var stagedOutput = writer.StageReplace(outputFile, globalCoverageInfo);
        stagedOutput.Commit();
        return true;
    }

    private static bool TryLoadAndCombine(
        string inputFolder,
        string outputFile,
        out GlobalCoverageInfo globalCoverageInfo,
        bool useStdOut)
    {
        globalCoverageInfo = default;

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
            if (!GlobalCoverageFileCombiner.TryAcquireInputFiles(inputFolder, expectedRunToken: null, out jsonFiles))
            {
                return false;
            }

            if (jsonFiles.Length == 0)
            {
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

        Action<string> onFileProcessed = useStdOut ? file => Utils.WriteSuccess($"Processing: {file}") : null;
        if (!GlobalCoverageFileCombiner.TryCombine(
                jsonFiles,
                outputFile,
                requireAllInputs: false,
                onFileProcessed: onFileProcessed,
                out globalCoverageInfo,
                out var rejectedInput))
        {
            if (useStdOut && rejectedInput is not null)
            {
                Utils.WriteError($"Error processing {rejectedInput}");
            }

            return false;
        }

        return true;
    }
}
