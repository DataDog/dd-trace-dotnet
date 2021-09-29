// <copyright file="BenchmarkParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Nuke.Common;

namespace BenchmarkComparison
{
    public static class BenchmarkParser
    {
        private const string FullBdnJsonFileExtension = "full-compressed.json";
        private const string BdnCsvFileExtension = ".csv";

        public static List<BdnRunSummary> ReadCsvResults(string path)
        {
            var files = GetFilesToParse(path, BdnCsvFileExtension);
            return files.Select(ReadFromCsvFile).ToList();
        }

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
                Logger.Error($"Exception reading benchmarkdotnet json results '{resultFilePath}': {ex}");
                throw;
            }
        }

        private static BdnRunSummary ReadFromCsvFile(string resultFilePath)
        {
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                };
                using var reader = new StreamReader(resultFilePath);
                using var csv = new CsvReader(reader, config);
                var summaries = csv.GetRecords<BdnBenchmarkSummary>().ToList();
                var name = Path.GetFileName(resultFilePath).Replace("-report.csv", "");
                return new BdnRunSummary(name, summaries);
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception reading benchmarkdotnet csv results '{resultFilePath}': {ex}");
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
