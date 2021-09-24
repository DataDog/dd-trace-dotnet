// <copyright file="CompareBenchmarks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.Utilities.Collections;
using Perfolizer.Mathematics.Multimodality;
using Perfolizer.Mathematics.SignificanceTesting;
using Perfolizer.Mathematics.Thresholds;

namespace BenchmarkComparison
{
    public static class CompareBenchmarks
    {
        private const string FullBdnJsonFileExtension = "full-compressed.json";
        private const string BdnCsvFileExtension = ".csv";
        private static readonly Threshold StatisticalTestThreshold = Threshold.Create(ThresholdUnit.Ratio, 0.05);
        private static readonly Threshold NoiseThreshold = Threshold.Create(ThresholdUnit.Nanoseconds, 0.3);
        private const int ShowTopNResults = 10;

        public static string GetMarkdown(string masterDir, string prDir, int prNumber, string oldCommit)
        {
            var oldBranchMarkdown = $"[master](https://github.com/DataDog/dd-trace-dotnet/tree/{oldCommit})";
            var newBranchMarkdown = $"#{prNumber}";

            var baseJsonResults = ReadJsonResults(masterDir);
            var prJsonResults = ReadJsonResults(prDir);

            var baseCsvResults = ReadCsvResults(masterDir);
            var prCsvResults = ReadCsvResults(prDir);

            var comparison = MatchResults(baseJsonResults, prJsonResults, baseCsvResults, prCsvResults);

            return BuildMarkdown(comparison, newBranchMarkdown, oldBranchMarkdown);
        }

        static string BuildMarkdown(List<MatchedSummary> comparison, string newBranchMarkdown, string oldBranchMarkdown)
        {
            var sb = new StringBuilder(
                $@"## Benchmarks Report :snail:

Benchmarks for {newBranchMarkdown} compared to {oldBranchMarkdown}: 
");
            WriteChangesSummary(comparison, sb);

            sb.AppendLine()
              .AppendLine(@"### Benchmark details")
              .AppendLine();

            foreach (var summary in (IEnumerable<MatchedSummary>)comparison)
            {
                var conclusion = summary.Conclusion;

                sb.AppendLine()
                  .AppendLine($"<details><summary><strong>{summary.BenchmarkName}</strong> - {GetDescriptionAndIcon(conclusion)}")
                  .AppendLine();

                if (conclusion == EquivalenceTestConclusion.Faster || conclusion == EquivalenceTestConclusion.Slower)
                {
                    WriteChangesTable(summary.Comparisons, EquivalenceTestConclusion.Slower, sb, newBranchMarkdown);
                    WriteChangesTable(summary.Comparisons, EquivalenceTestConclusion.Faster, sb, newBranchMarkdown);
                }

                sb.AppendLine(
                    @"</summary>

### Raw results
");

                sb.AppendLine(
                    @"
|  Branch |                      Method |     Toolchain |     Mean |    Error |  StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|---------|---------------------------- |-------------- |---------:|---------:|--------:|-------:|------:|------:|----------:|");

                TryWriteSummaryRow(sb, oldBranchMarkdown, summary.BaseSummary);
                TryWriteSummaryRow(sb, newBranchMarkdown, summary.DiffSummary);

                sb.AppendLine("</details>")
                  .AppendLine();
            }

            static void TryWriteSummaryRow(StringBuilder sb1, string branchMarkdown, List<BdnBenchmarkSummary> summaries)
            {
                if (summaries is null || !summaries.Any())
                {
                    return;
                }

                foreach (var run in summaries)
                {
                    sb1.AppendLine($@"|{branchMarkdown}|`{run.Method}`|{run.Toolchain}|{NoBr(run.Mean)}|{NoBr(run.Error)}|{NoBr(run.StdDev)}|{run.Gen0}| {run.Gen1}|{run.Gen2}|{NoBr(run.Allocated)}|");
                }
            }

            return sb.ToString();


            static void WriteChangesSummary(
                IEnumerable<MatchedSummary> comparison,
                StringBuilder stringBuilder)
            {
                var better = comparison.SelectMany(x => x.Comparisons).Where(result => result.Conclusion == EquivalenceTestConclusion.Faster);
                var worse = comparison.SelectMany(x => x.Comparisons).Where(result => result.Conclusion == EquivalenceTestConclusion.Slower);
                var betterCount = better.Count();
                var worseCount = worse.Count();

                // If the baseline doesn't have the same set of tests, you wind up with Infinity in the list of diffs.
                // Exclude them for purposes of geomean.
                worse = worse.Where(x => GetRatio(x) != double.PositiveInfinity);
                better = better.Where(x => GetRatio(x) != double.PositiveInfinity);

                if (betterCount > 0)
                {
                    var betterGeoMean = Math.Pow(10, better.Skip(1).Aggregate(Math.Log10(GetRatio(better.First())), (x, y) => x + Math.Log10(GetRatio(y))) / better.Count());
                    stringBuilder.AppendLine($"* {betterCount} benchmarks are **better**, with geometric mean {betterGeoMean:F3}");
                }

                if (worseCount > 0)
                {
                    var worseGeoMean = Math.Pow(10, worse.Skip(1).Aggregate(Math.Log10(GetRatio(worse.First())), (x, y) => x + Math.Log10(GetRatio(y))) / worse.Count());
                    stringBuilder.AppendLine($"* {worseCount} benchmarks are **worse**, with geometric mean {worseGeoMean:F3}");
                }

                if (betterCount == 0 && worseCount == 0)
                {
                    stringBuilder.AppendLine($"* All benchmarks are **the same**");
                }
            }

            static void WriteChangesTable(
                IEnumerable<BenchmarkComparison> results,
                EquivalenceTestConclusion conclusion,
                StringBuilder sb,
                string newBranchMarkdown)
            {
                var data = results
                          .Where(result => result.Conclusion == conclusion)
                          .OrderByDescending(result => GetRatio(conclusion, result.BaseResult, result.DiffResult))
                          .Take(ShowTopNResults)
                          .Select(
                               result => new
                               {
                                   Id = result.Id,
                                   DisplayValue = GetRatio(conclusion, result.BaseResult, result.DiffResult),
                                   BaseMedian = result.BaseResult.Statistics.Median,
                                   DiffMedian = result.DiffResult.Statistics.Median,
                                   Modality = GetModalInfo(result.BaseResult) ?? GetModalInfo(result.DiffResult)
                               })
                          .ToArray();

                if (!data.Any())
                {
                    return;
                }

                var diffTitle = conclusion == EquivalenceTestConclusion.Faster ? "base/diff" : "diff/base";
                sb.AppendLine(
                    $@"

#### {GetDescriptionAndIcon(conclusion)} in {newBranchMarkdown}

|  Benchmark         | {diffTitle} | Base Median (ns) | Diff Median (ns) | Modality   |
|:----------|-----------:|-----------:|--------:|--------:|");
                foreach (var datum in data)
                {
                    sb.AppendLine($@"| {NoBr(datum.Id)} | {datum.DisplayValue:N3} | {datum.BaseMedian:N2} | {datum.DiffMedian:N2} | {datum.Modality}");
                }

                sb.AppendLine();
            }

            static string GetDescriptionAndIcon(EquivalenceTestConclusion conclusion) => conclusion switch
            {
                EquivalenceTestConclusion.Faster => "Faster :tada:",
                EquivalenceTestConclusion.Slower => "Slower :warning:",
                EquivalenceTestConclusion.Same => "No change :heavy_check_mark:",
                _ => "Unknown :shrug:",
            };

            static string NoBr(string value) => value
                                               .Replace(" ", "&nbsp;")
                                               .Replace("-", "&#8209;");
        }

        private static List<MatchedSummary> MatchResults(
            IEnumerable<BdnResult> baseResults,
            IEnumerable<BdnResult> diffResults,
            IEnumerable<BdnRunSummary> baseSummary,
            IEnumerable<BdnRunSummary> diffSummary)
        {
            var baseResultsByFilename = baseResults.ToDictionary(x => x.FileName, x => x);
            var diffResultsByFilename = diffResults.ToDictionary(x => x.FileName, x => x);
            var baseSummaryByFilename = baseSummary.ToDictionary(x => x.FileName, x => x);
            var diffSummaryByFilename = diffSummary.ToDictionary(x => x.FileName, x => x);

            var keys = baseResultsByFilename.Keys
                                            .Concat(diffResultsByFilename.Keys)
                                            .Concat(baseSummaryByFilename.Keys)
                                            .Concat(diffSummaryByFilename.Keys)
                                            .Distinct()
                                            .OrderBy(x => x);

            return keys
                  .Select(key =>
                   {
                       var baseResult = GetValueOrDefault(baseResultsByFilename, key);
                       var diffResult = GetValueOrDefault(diffResultsByFilename, key);
                       var baseSummary = GetValueOrDefault(baseSummaryByFilename, key);
                       var diffSummary = GetValueOrDefault(diffSummaryByFilename, key);

                       if (baseResult is null || diffResult is null)
                       {
                           return new MatchedSummary(key,
                                                     baseSummary?.Results ?? new List<BdnBenchmarkSummary>(),
                                                     diffSummary?.Results ?? new List<BdnBenchmarkSummary>(),
                                                     new List<BenchmarkComparison>());
                       }

                       var baseBenchmarksByName = baseResult.Benchmarks.ToDictionary(GetName, x => x);
                       var diffBenchmarksByName = diffResult.Benchmarks.ToDictionary(GetName, x => x);

                       var benchmarkKeys = baseBenchmarksByName.Keys.Concat(diffBenchmarksByName.Keys).Distinct();
                       var comparisons = benchmarkKeys
                                        .Select(id =>
                                         {
                                             var baseBenchmark = GetValueOrDefault(baseBenchmarksByName, id);
                                             var diffBenchmark = GetValueOrDefault(diffBenchmarksByName, id);

                                             return new BenchmarkComparison(id, baseBenchmark, diffBenchmark, EquivalenceTestConclusion.Unknown);
                                         })
                                        .Compare(StatisticalTestThreshold, NoiseThreshold)
                                        .ToList();

                       return new MatchedSummary(key, baseSummary.Results, diffSummary.Results, comparisons);
                   })
                  .ToList();

            static string GetName(Benchmark benchmark) => benchmark.DisplayInfo.Contains("Toolchain=net472")
                                                       ? $"{benchmark.FullName}-net472"
                                                       : $"{benchmark.FullName}-netcoreapp3.1";

            static T GetValueOrDefault<T>(Dictionary<string, T> dict, string key)
                => dict.TryGetValue(key, out var value) ? value : default;
        }

        private static IEnumerable<BenchmarkComparison> Compare(
            this IEnumerable<BenchmarkComparison> results,
            Threshold testThreshold,
            Threshold noiseThreshold)
        {
            foreach (var result in results)
            {
                if (result.BaseResult?.Statistics is null || result.DiffResult?.Statistics is null)
                {
                    yield return result with { Conclusion = EquivalenceTestConclusion.Unknown };
                    continue;
                }

                var baseValues = result.BaseResult.GetOriginalValues();
                var diffValues = result.DiffResult.GetOriginalValues();

                var userTresholdResult = StatisticalTestHelper.CalculateTost(MannWhitneyTest.Instance, baseValues, diffValues, testThreshold);
                var conclusion = userTresholdResult.Conclusion switch
                {
                    EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ when StatisticalTestHelper.CalculateTost(MannWhitneyTest.Instance, baseValues, diffValues, noiseThreshold).Conclusion == EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ => userTresholdResult.Conclusion,
                };

                yield return result with { Conclusion = conclusion };
            }
        }

        // code and magic values taken from BenchmarkDotNet.Analysers.MultimodalDistributionAnalyzer
        // See http://www.brendangregg.com/FrequencyTrails/modes.html
        private static string GetModalInfo(Benchmark benchmark)
        {
            if (benchmark.Statistics.N < 12) // not enough data to tell
                return null;

            double mValue = MValueCalculator.Calculate(benchmark.GetOriginalValues());
            if (mValue > 4.2)
                return "multimodal";
            else if (mValue > 3.2)
                return "bimodal";
            else if (mValue > 2.8)
                return "several?";

            return null;
        }

        private static double GetRatio(BenchmarkComparison item) => GetRatio(item.Conclusion, item.BaseResult, item.DiffResult);

        private static double GetRatio(EquivalenceTestConclusion conclusion, Benchmark baseResult, Benchmark diffResult)
            => conclusion == EquivalenceTestConclusion.Faster
                ? baseResult.Statistics.Median / diffResult.Statistics.Median
                : diffResult.Statistics.Median / baseResult.Statistics.Median;


        private static string[] GetFilesToParse(string path, string extension)
        {
            if (Directory.Exists(path))
                return Directory.GetFiles(path, $"*{extension}", SearchOption.AllDirectories);
            else if (File.Exists(path) || !path.EndsWith(extension))
                return new[] { path };
            else
                throw new FileNotFoundException($"Provided path does NOT exist or is not a {path} file", path);
        }

        private static List<BdnRunSummary> ReadCsvResults(string path)
        {
            var files = GetFilesToParse(path, BdnCsvFileExtension);
            return files.Select(ReadFromCsvFile).ToList();
        }

        private static List<BdnResult> ReadJsonResults(string path)
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

        // https://stackoverflow.com/a/6907849/5852046 not perfect but should work for all we need
        // private static string WildcardToRegex(string pattern) => $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$";

        public class ChronometerFrequency
        {
            public int Hertz { get; set; }
        }

        public class HostEnvironmentInfo
        {
            public string BenchmarkDotNetCaption { get; set; }
            public string BenchmarkDotNetVersion { get; set; }
            public string OsVersion { get; set; }
            public string ProcessorName { get; set; }
            public int? PhysicalProcessorCount { get; set; }
            public int? PhysicalCoreCount { get; set; }
            public int? LogicalCoreCount { get; set; }
            public string RuntimeVersion { get; set; }
            public string Architecture { get; set; }
            public bool? HasAttachedDebugger { get; set; }
            public bool? HasRyuJit { get; set; }
            public string Configuration { get; set; }
            public string JitModules { get; set; }
            public string DotNetCliVersion { get; set; }
            public ChronometerFrequency ChronometerFrequency { get; set; }
            public string HardwareTimerKind { get; set; }
        }

        public class ConfidenceInterval
        {
            public int N { get; set; }
            public double Mean { get; set; }
            public double StandardError { get; set; }
            public int Level { get; set; }
            public double Margin { get; set; }
            public double Lower { get; set; }
            public double Upper { get; set; }
        }

        public class Percentiles
        {
            public double P0 { get; set; }
            public double P25 { get; set; }
            public double P50 { get; set; }
            public double P67 { get; set; }
            public double P80 { get; set; }
            public double P85 { get; set; }
            public double P90 { get; set; }
            public double P95 { get; set; }
            public double P100 { get; set; }
        }

        public class Statistics
        {
            public int N { get; set; }
            public double Min { get; set; }
            public double LowerFence { get; set; }
            public double Q1 { get; set; }
            public double Median { get; set; }
            public double Mean { get; set; }
            public double Q3 { get; set; }
            public double UpperFence { get; set; }
            public double Max { get; set; }
            public double InterquartileRange { get; set; }
            public List<double> LowerOutliers { get; set; }
            public List<double> UpperOutliers { get; set; }
            public List<double> AllOutliers { get; set; }
            public double StandardError { get; set; }
            public double Variance { get; set; }
            public double StandardDeviation { get; set; }
            public double? Skewness { get; set; }
            public double? Kurtosis { get; set; }
            public ConfidenceInterval ConfidenceInterval { get; set; }
            public Percentiles Percentiles { get; set; }
        }

        public class Memory
        {
            public int Gen0Collections { get; set; }
            public int Gen1Collections { get; set; }
            public int Gen2Collections { get; set; }
            public long TotalOperations { get; set; }
            public long BytesAllocatedPerOperation { get; set; }
        }

        public class Measurement
        {
            public string IterationStage { get; set; }
            public int LaunchIndex { get; set; }
            public int IterationIndex { get; set; }
            public long Operations { get; set; }
            public double Nanoseconds { get; set; }
        }

        public class Benchmark
        {
            public string DisplayInfo { get; set; }
            public object Namespace { get; set; }
            public string Type { get; set; }
            public string Method { get; set; }
            public string MethodTitle { get; set; }
            public string Parameters { get; set; }
            public string FullName { get; set; }
            public Statistics Statistics { get; set; }
            public Memory Memory { get; set; }
            public List<Measurement> Measurements { get; set; }

            /// <summary>
            /// this method was not auto-generated by a tool, it was added manually
            /// </summary>
            /// <returns>an array of the actual workload results (not warmup, not pilot)</returns>
            internal double[] GetOriginalValues()
                => Measurements
                    .Where(measurement => measurement.IterationStage == "Result")
                    .Select(measurement => measurement.Nanoseconds / measurement.Operations)
                    .ToArray();
        }

        public class BdnResult
        {
            public string FileName { get; set; }
            public string Title { get; set; }
            public HostEnvironmentInfo HostEnvironmentInfo { get; set; }
            public List<Benchmark> Benchmarks { get; set; }
        }

        public record MatchedSummary(
            string BenchmarkName,
            List<BdnBenchmarkSummary> BaseSummary,
            List<BdnBenchmarkSummary> DiffSummary,
            List<BenchmarkComparison> Comparisons)
        {
            public EquivalenceTestConclusion Conclusion
            {
                get
                {
                    if (Comparisons.Any(x => x.Conclusion == EquivalenceTestConclusion.Slower))
                    {
                        return EquivalenceTestConclusion.Slower;
                    }
                    else if (Comparisons.Any(x => x.Conclusion == EquivalenceTestConclusion.Faster))
                    {
                        return EquivalenceTestConclusion.Faster;
                    }
                    else if (Comparisons.All(x => x.Conclusion == EquivalenceTestConclusion.Same))
                    {
                        return EquivalenceTestConclusion.Same;
                    }
                    else
                    {
                        return EquivalenceTestConclusion.Unknown;
                    }
                }
            }
        };

        public record BenchmarkComparison(
            string Id,
            Benchmark BaseResult,
            Benchmark DiffResult,
            EquivalenceTestConclusion Conclusion);

        public record BdnRunSummary(string FileName, List<BdnBenchmarkSummary> Results);

        public class BdnBenchmarkSummary
        {
            public string Method { get; set; }
            public string Job { get; set; }
            public string Runtime { get; set; }
            public string Toolchain { get; set; }
            public string IterationTime { get; set; }
            public string Mean { get; set; }
            public string Error { get; set; }
            public string StdDev { get; set; }
            public string Ratio { get; set; }
            [Name("Gen 0")]
            public string Gen0 { get; set; }
            [Name("Gen 1")]
            public string Gen1 { get; set; }
            [Name("Gen 2")]
            public string Gen2 { get; set; }
            public string Allocated { get; set; }
        }
    }
}
