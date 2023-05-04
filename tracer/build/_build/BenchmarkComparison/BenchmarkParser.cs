// <copyright file="BenchmarkParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Logger = Serilog.Log;

namespace BenchmarkComparison
{
    public static class BenchmarkParser
    {
        private const string FullBdnJsonFileExtension = "full-compressed.json";

        public static List<BdnResult> ReadJsonResults(string path)
        {
            var files = GetFilesToParse(path, FullBdnJsonFileExtension);
            return files.Select(ReadFromJsonFile).ToList();
        }

        private static BdnResult ReadFromJsonFile(string resultFilePath)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<BdnResult>(File.ReadAllText(resultFilePath));
                result.FileName = Path.GetFileName(resultFilePath).Replace($"-report-{FullBdnJsonFileExtension}", "");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception reading benchmarkdotnet json results '{ResultFilePath}'", resultFilePath);
                throw;
            }
        }

        private static string[] GetFilesToParse(string path, string extension)
        {
            if (Directory.Exists(path))
                return Directory.GetFiles(path, $"*{extension}", SearchOption.AllDirectories);
            else if (File.Exists(path) || !path.EndsWith(extension))
                return new[] { path };
            else
                throw new FileNotFoundException($"Provided path does NOT exist or is not a {path} file", path);
        }

    }
}
