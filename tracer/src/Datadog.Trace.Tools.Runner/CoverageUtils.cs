// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Vendors.Newtonsoft.Json;
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

        if (!TryCombineAndGetTotalCoverage(inputFolder, out globalCoverageInfo, useStdOut))
        {
            return false;
        }

        using var fStream = File.OpenWrite(outputFile);
        using var sWriter = new StreamWriter(fStream, Encoding.UTF8, 4096, false);
        if (useStdOut)
        {
            Utils.WriteSuccess($"Writing {outputFile}");
        }

        new JsonSerializer().Serialize(sWriter, globalCoverageInfo);
        return true;
    }

    private static bool TryCombineAndGetTotalCoverage(string inputFolder, out GlobalCoverageInfo globalCoverageInfo, bool useStdOut = true)
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
            jsonFiles = Directory.GetFiles(inputFolder, "*.json", SearchOption.TopDirectoryOnly);
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

        List<GlobalCoverageInfo> globalCoverages = new();
        foreach (var file in jsonFiles)
        {
            var fileContent = File.ReadAllText(file);
            try
            {
                if (JsonConvert.DeserializeObject<GlobalCoverageInfo>(fileContent) is { } gCoverageInfo)
                {
                    if (useStdOut)
                    {
                        Utils.WriteSuccess($"Processing: {file}");
                    }

                    globalCoverages.Add(gCoverageInfo);
                }
                else if (useStdOut)
                {
                    Utils.WriteSuccess($"Ignored: {file}");
                }
            }
            catch (Exception ex)
            {
                Utils.WriteError($"Error processing {file}");
                AnsiConsole.WriteException(ex);
            }
        }

        globalCoverageInfo = GlobalCoverageInfo.Combine(globalCoverages.ToArray());
        return true;
    }
}
