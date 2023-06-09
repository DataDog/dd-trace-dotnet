// <copyright file="BenchmarkComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer
// using System.Collections.Generic;

using System;
using System.Collections.Generic;
using System.Linq;
using ByteSizeLib;
using Perfolizer.Mathematics.SignificanceTesting;
using Perfolizer.Mathematics.Thresholds;

namespace BenchmarkComparison
{
    public static class BenchmarkComparer
    {
        public static readonly Threshold SignificantResultThreshold = Threshold.Create(ThresholdUnit.Ratio, 0.10);
        public static readonly Threshold NoiseThreshold = Threshold.Create(ThresholdUnit.Nanoseconds, 0.3);
        public static readonly decimal AllocationThresholdRatio = 0.005M; //0.5%

        public static List<MatchedSummary> MatchAndCompareResults(
            IEnumerable<BdnResult> baseResults,
            IEnumerable<BdnResult> diffResults)
        {
            var baseResultsByFilename = baseResults.ToDictionary(x => x.FileName, x => x);
            var diffResultsByFilename = diffResults.ToDictionary(x => x.FileName, x => x);

            var keys = baseResultsByFilename.Keys
                                            .Concat(diffResultsByFilename.Keys)
                                            .Distinct()
                                            .OrderBy(x => x);

            return keys
                  .Select(key =>
                   {
                       var baseResult = GetValueOrDefault(baseResultsByFilename, key);
                       var diffResult = GetValueOrDefault(diffResultsByFilename, key);

                       if (baseResult is null || diffResult is null)
                       {
                           return new MatchedSummary(key,
                                                     baseResult?.Benchmarks ?? new List<Benchmark>(),
                                                     diffResult?.Benchmarks ?? new List<Benchmark>(),
                                                     new List<BenchmarkComparison>(),
                                                     new List<AllocationComparison>());
                       }

                       var baseBenchmarksByName = baseResult.Benchmarks.ToDictionary(GetName, x => x);
                       var diffBenchmarksByName = diffResult.Benchmarks.ToDictionary(GetName, x => x);

                       var benchmarkKeys = baseBenchmarksByName.Keys.Concat(diffBenchmarksByName.Keys).Distinct();

                       var matchedBenchmarks = benchmarkKeys
                                              .Select(id =>
                                               {
                                                   var baseBenchmark = GetValueOrDefault(baseBenchmarksByName, id);
                                                   var diffBenchmark = GetValueOrDefault(diffBenchmarksByName, id);
                                                   if (baseBenchmark is null || diffBenchmark is null)
                                                   {
                                                       return default;
                                                   }

                                                   return (Id: id, Base: baseBenchmark, Diff: diffBenchmark);
                                               })
                                              .Where(i => i.Id is not null)
                                              .ToList();
                       var benchmarkComparisons = matchedBenchmarks
                                                 .Select(x => new BenchmarkComparison(x.Id, x.Base, x.Diff, EquivalenceTestConclusion.Unknown))
                                                 .Compare(SignificantResultThreshold, NoiseThreshold)
                                                 .ToList();

                       var allocationComparisons = matchedBenchmarks
                                                  .Select(x => new AllocationComparison(x.Id, x.Base, x.Diff, AllocationConclusion.Unknown))
                                                  .CompareAllocations(AllocationThresholdRatio)
                                                  .ToList();

                       return new MatchedSummary(key, baseResult.Benchmarks, diffResult.Benchmarks, benchmarkComparisons, allocationComparisons);
                   })
                  .ToList();

            static string GetName(Benchmark benchmark)
            {
                if (benchmark.DisplayInfo.Contains("Toolchain=net472"))
                {
                    return $"{benchmark.FullName}-net472";
                }

                if (benchmark.DisplayInfo.Contains("Toolchain=net6.0"))
                {
                    return $"{benchmark.FullName}-net6.0";
                }

                return $"{benchmark.FullName}-netcoreapp3.1";
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
            decimal thresholdRatio)
        {
            foreach (var result in results)
            {
                var baseBytes = result.BaseResult?.Memory.BytesAllocatedPerOperation;
                var diffBytes = result.DiffResult?.Memory.BytesAllocatedPerOperation;
                if (baseBytes is null || diffBytes is null)
                {
                    yield return result with { Conclusion = AllocationConclusion.Unknown };
                    continue;
                }

                var difference = diffBytes.Value - baseBytes.Value;
                // handle divide by zero
                var differenceRatio = (baseBytes, difference) switch
                {
                    (_, 0) => 0M,
                    (0, _) => 100M,
                    _ => Convert.ToDecimal(difference) / Convert.ToDecimal(baseBytes),
                };

                var conclusion = differenceRatio switch
                {
                    var x when x < -thresholdRatio => AllocationConclusion.FewerAllocations,
                    var y when y > thresholdRatio => AllocationConclusion.MoreAllocations,
                    _ => AllocationConclusion.Same
                };

                yield return result with { Conclusion = conclusion };
            }
        }
    }
}
