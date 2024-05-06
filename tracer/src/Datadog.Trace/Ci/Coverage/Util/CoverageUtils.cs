// <copyright file="CoverageUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

internal static class CoverageUtils
{
    internal static readonly IDatadogLogger Log = Datadog.Trace.Ci.CIVisibility.Log;

    public static bool TryCombineAndGetTotalCoverage(string inputFolder, string outputFile)
    {
        return TryCombineAndGetTotalCoverage(inputFolder, outputFile, out _);
    }

    public static bool TryCombineAndGetTotalCoverage(string? inputFolder, string? outputFile, out GlobalCoverageInfo? globalCoverageInfo)
    {
        if (string.IsNullOrEmpty(outputFile))
        {
            globalCoverageInfo = null;
            return false;
        }

        if (!TryCombineAndGetTotalCoverage(inputFolder, out globalCoverageInfo))
        {
            return false;
        }

        try
        {
            using var fStream = File.OpenWrite(outputFile);
            using var sWriter = new StreamWriter(fStream, Encoding.UTF8, 4096, false);
            new JsonSerializer().Serialize(sWriter, globalCoverageInfo);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error writing output file: {File}", outputFile);
        }

        return false;
    }

    private static bool TryCombineAndGetTotalCoverage(string? inputFolder, out GlobalCoverageInfo? globalCoverageInfo)
    {
        globalCoverageInfo = default;

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

            var jsonFiles = Directory.GetFiles(inputFolder, "*.json", SearchOption.TopDirectoryOnly);
            if (jsonFiles.Length == 0)
            {
                Log.Error("'{InputFolder}' doesn't contain any json file.", inputFolder);
                return false;
            }

            List<GlobalCoverageInfo> globalCoverages = new();
            foreach (var file in jsonFiles)
            {
                var fileContent = File.ReadAllText(file);
                try
                {
                    if (JsonConvert.DeserializeObject<GlobalCoverageInfo>(fileContent) is { } gCoverageInfo)
                    {
                        globalCoverages.Add(gCoverageInfo);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing {File}", file);
                }
            }

            globalCoverageInfo = GlobalCoverageInfo.Combine(globalCoverages.ToArray());
            return true;
        }
        catch (Exception globalEx)
        {
            Log.Error(globalEx, "Error combining all code coverages for the folder: {Folder}", inputFolder);
        }

        return false;
    }
}
