// <copyright file="BenchmarkComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer
// using System.Collections.Generic;

using System.Collections.Generic;
using System.Linq;
using ByteSizeLib;
using Perfolizer.Mathematics.Multimodality;
using Perfolizer.Mathematics.SignificanceTesting;
using Perfolizer.Mathematics.Thresholds;

namespace BenchmarkComparison
{
    public static class BenchmarkComparer
    {
        public static readonly Threshold SignificantResultThreshold = Threshold.Create(ThresholdUnit.Ratio, 0.10);
        public static readonly Threshold NoiseThreshold = Threshold.Create(ThresholdUnit.Nanoseconds, 0.3);
        public static readonly double AllocationThresholdPercent = 0.5;//%

        public static List<MatchedSummary> MatchAndCompareResults(
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

                       if (baseResult is null || diffResult is null || baseSummary is null || diffSummary is null)
                       {
                           return new MatchedSummary(key,
                                                     baseSummary?.Results ?? new List<BdnBenchmarkSummary>(),
                                                     diffSummary?.Results ?? new List<BdnBenchmarkSummary>(),
                                                     new List<BenchmarkComparison>(),
                                                     new List<AllocationComparison>());
                       }

                       var baseBenchmarksByName = baseResult.Benchmarks.ToDictionary(GetName, x => x);
                       var diffBenchmarksByName = diffResult.Benchmarks.ToDictionary(GetName, x => x);

                       var benchmarkKeys = baseBenchmarksByName.Keys.Concat(diffBenchmarksByName.Keys).Distinct();
                       var benchmarkComparisons = benchmarkKeys
                                        .Select(id =>
                                         {
                                             var baseBenchmark = GetValueOrDefault(baseBenchmarksByName, id);
                                             var diffBenchmark = GetValueOrDefault(diffBenchmarksByName, id);

                                             return new BenchmarkComparison(id, baseBenchmark, diffBenchmark, EquivalenceTestConclusion.Unknown);
                                         })
                                        .Compare(SignificantResultThreshold, NoiseThreshold)
                                        .ToList();

                       var baseSummariesByName = baseSummary.Results
                                                           .ToDictionary(b => $"{b.Method}-{b.Toolchain}", x => x);

                       var diffSummariesByName = diffSummary.Results
                                                           .ToDictionary(b => $"{b.Method}-{b.Toolchain}", x => x);
                       var summaryKeys = baseSummariesByName.Keys.Concat(diffSummariesByName.Keys).Distinct();

                       var allocationComparisons = summaryKeys
                                                  .Select(id =>
                                                   {

                                                       var baseBenchmark = GetValueOrDefault(baseSummariesByName, id);
                                                       var diffBenchmark = GetValueOrDefault(diffSummariesByName, id);

                                                       return new AllocationComparison(id, baseBenchmark, diffBenchmark, AllocationConclusion.Unknown);
                                                   })
                                                  .CompareAllocations(AllocationThresholdPercent)
                                                  .ToList();

                       return new MatchedSummary(key, baseSummary.Results, diffSummary.Results, benchmarkComparisons, allocationComparisons);
                   })
                  .ToList();


            static string GetName(Benchmark benchmark)
            {
                var name = benchmark.FullName;

                if (!string.IsNullOrEmpty(benchmark.Parameters))
                {
                    name += $"-{benchmark.Parameters}";
                }

                name += $"-{(benchmark.DisplayInfo.Contains("Toolchain=net472") ? "net472" : "netcoreapp3.1")}";

                return name;
            }

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

        private static IEnumerable<AllocationComparison> CompareAllocations(
            this IEnumerable<AllocationComparison> results,
            double thresholdPercent)
        {
            foreach (var result in results)
            {
                if (!ByteSize.TryParse(result.BaseResult.Allocated, out var baseBytes)
                 || !ByteSize.TryParse(result.DiffResult.Allocated, out var diffBytes))
                {
                    yield return result with { Conclusion = AllocationConclusion.Unknown };
                    continue;
                }

                var zero = ByteSize.FromBytes(0);
                var difference = diffBytes - baseBytes;
                // handle divide by zero
                var percentageDifference = difference == zero ? 0 : baseBytes.Bytes / difference.Bytes;

                var conclusion = percentageDifference switch
                {
                    var x when x < -thresholdPercent => AllocationConclusion.FewerAllocations,
                    var y when y > thresholdPercent => AllocationConclusion.MoreAllocations,
                    _ => AllocationConclusion.Same
                };

                yield return result with { Conclusion = conclusion };
            }
        }
    }
}
