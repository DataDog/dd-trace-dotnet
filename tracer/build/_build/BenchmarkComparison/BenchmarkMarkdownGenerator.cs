// <copyright file="BenchmarkMarkdownGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ByteSizeLib;
using Perfolizer.Mathematics.Multimodality;
using Perfolizer.Mathematics.SignificanceTesting;

namespace BenchmarkComparison
{
    public static class BenchmarkMarkdownGenerator
    {
        private const int ShowTopNResults = 10;

        public static string GetMarkdown(List<MatchedSummary> comparison, string oldBranchMarkdown, string newBranchMarkdown)
        {
            var sb = new StringBuilder(
                $@"## Benchmarks Report :snail:

Benchmarks for {newBranchMarkdown} compared to {oldBranchMarkdown}: 
");
            WriteChangesSummary(comparison, sb);

            WriteThresholdsSummary(sb);

            sb.AppendLine()
              .AppendLine(@"### Benchmark details")
              .AppendLine();

            foreach (var summary in (IEnumerable<MatchedSummary>)comparison)
            {
                var conclusion = summary.Conclusion;
                var allocationConclusion = summary.AllocationConclusion;

                sb.AppendLine()
                  .AppendLine($"<details><summary><strong>{summary.BenchmarkName}</strong> - {GetSpeedDescriptionAndIcon(conclusion)} {GetAllocationDescriptionAndIcon(allocationConclusion)}")
                  .AppendLine();

                if (conclusion == EquivalenceTestConclusion.Faster || conclusion == EquivalenceTestConclusion.Slower)
                {
                    WriteSpeedChangesTable(summary.Comparisons, EquivalenceTestConclusion.Slower, sb, newBranchMarkdown);
                    WriteSpeedChangesTable(summary.Comparisons, EquivalenceTestConclusion.Faster, sb, newBranchMarkdown);
                }

                if (allocationConclusion == AllocationConclusion.FewerAllocations || allocationConclusion == AllocationConclusion.MoreAllocations)
                {
                    WriteAllocationChangesTable(summary.AllocationComparisons, AllocationConclusion.MoreAllocations, sb, newBranchMarkdown);
                    WriteAllocationChangesTable(summary.AllocationComparisons, AllocationConclusion.FewerAllocations, sb, newBranchMarkdown);
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
        }

        private static void WriteChangesSummary(
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
                stringBuilder.AppendLine($"* {betterCount} benchmarks are **faster**, with geometric mean {betterGeoMean:F3}");
            }

            if (worseCount > 0)
            {
                var worseGeoMean = Math.Pow(10, worse.Skip(1).Aggregate(Math.Log10(GetRatio(worse.First())), (x, y) => x + Math.Log10(GetRatio(y))) / worse.Count());
                stringBuilder.AppendLine($"* {worseCount} benchmarks are **slower**, with geometric mean {worseGeoMean:F3}");
            }

            if (betterCount == 0 && worseCount == 0)
            {
                stringBuilder.AppendLine($"* All benchmarks have **the same** speed");
            }

            var betterAlloc = comparison.SelectMany(x => x.AllocationComparisons).Where(result => result.Conclusion == AllocationConclusion.FewerAllocations).ToList();
            var worseAlloc = comparison.SelectMany(x => x.AllocationComparisons).Where(result => result.Conclusion == AllocationConclusion.MoreAllocations).ToList();
            var betterAllocCount = betterAlloc.Count;
            var worseAllocCount = worseAlloc.Count;

            if (betterAllocCount > 0)
            {
                stringBuilder.AppendLine($"* {betterAllocCount} benchmarks have **fewer** allocations");
            }

            if (worseAllocCount > 0)
            {
                stringBuilder.AppendLine($"* {worseAllocCount} benchmarks have **more** allocations");
            }

            if (betterAllocCount == 0 && worseAllocCount == 0)
            {
                stringBuilder.AppendLine($"* All benchmarks have **the same** allocations");
            }
        }

        private static void WriteThresholdsSummary(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine($@"

The following thresholds were used for comparing the benchmark speeds:
* Mann–Whitney U test with statistical threshold of **{BenchmarkComparer.StatisticalTestThreshold}**
* When the above indicates significance, a Mann–Whitney U test with statistical threshold of **{BenchmarkComparer.StatisticalTestThreshold}** to reduce noise

Allocation changes below **{BenchmarkComparer.AllocationThresholdPercent:N1}%** are ignored.
");

        }

        private static void WriteSpeedChangesTable(
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

#### {GetSpeedDescriptionAndIcon(conclusion)} in {newBranchMarkdown}

|  Benchmark         | {diffTitle} | Base Median (ns) | Diff Median (ns) | Modality   |
|:----------|-----------:|-----------:|--------:|--------:|");
            foreach (var datum in data)
            {
                sb.AppendLine($@"| {NoBr(datum.Id)} | {datum.DisplayValue:N3} | {datum.BaseMedian:N2} | {datum.DiffMedian:N2} | {datum.Modality}");
            }

            sb.AppendLine();
        }

        private static void WriteAllocationChangesTable(
            IEnumerable<AllocationComparison> results,
            AllocationConclusion conclusion,
            StringBuilder sb,
            string newBranchMarkdown)
        {
            var data = results
                      .Where(result => result.Conclusion == conclusion)
                      .Select(
                           result =>
                           {
                               var baseSize = ByteSize.Parse(result.BaseResult.Allocated);
                               var diffSize = ByteSize.Parse(result.DiffResult.Allocated);
                               return new
                               {
                                   Id = result.Id,
                                   BaseAllocation = result.BaseResult.Allocated,
                                   DiffAllocation = result.DiffResult.Allocated,
                                   Change = (baseSize - diffSize).ToString(),
                                   PercentChange = (baseSize - diffSize).Bytes / baseSize.Bytes,
                               };
                           })
                      .OrderByDescending(result => result.PercentChange)
                      .Take(ShowTopNResults)
                      .ToArray();

            if (!data.Any())
            {
                return;
            }

            sb.AppendLine(
                $@"

#### {GetAllocationDescriptionAndIcon(conclusion)} in {newBranchMarkdown}

|  Benchmark         | Base Allocated | Diff Allocated | Change   | Change % |
|:----------|-----------:|-----------:|--------:|--------:|");
            foreach (var datum in data)
            {
                sb.AppendLine($@"| {NoBr(datum.Id)} | {datum.BaseAllocation} | {datum.DiffAllocation} | {datum.Change} | {datum.PercentChange:P2}");
            }

            sb.AppendLine();
        }

        private static string GetSpeedDescriptionAndIcon(EquivalenceTestConclusion conclusion) => conclusion switch
        {
            EquivalenceTestConclusion.Faster => "Faster :tada:",
            EquivalenceTestConclusion.Slower => "Slower :warning:",
            EquivalenceTestConclusion.Same => "Same speed :heavy_check_mark:",
            _ => "Unknown :shrug:",
        };

        private static string GetAllocationDescriptionAndIcon(AllocationConclusion conclusion) => conclusion switch
        {
            AllocationConclusion.FewerAllocations => "Fewer allocations :tada:",
            AllocationConclusion.MoreAllocations => "More allocations :warning:",
            AllocationConclusion.Same => "Same allocations :heavy_check_mark:",
            _ => "Unknown :shrug:",
        };

        private static string NoBr(string value) => value
                                                   .Replace(" ", "&nbsp;")
                                                   .Replace("-", "&#8209;");

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
    }
}
