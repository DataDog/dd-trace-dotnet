// <copyright file="CompareBenchmarks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/dotnet/performance/blob/ef497aa104ae7abe709c71fbb137230bf5be25e9/src/tools/ResultsComparer

namespace BenchmarkComparison
{
    public static class CompareBenchmarks
    {
        public static string GetMarkdown(string masterDir, string prDir, int prNumber, string oldCommit)
        {
            var oldBranchMarkdown = $"[master](https://github.com/DataDog/dd-trace-dotnet/tree/{oldCommit})";
            var newBranchMarkdown = $"#{prNumber}";

            var baseJsonResults = BenchmarkParser.ReadJsonResults(masterDir);
            var prJsonResults = BenchmarkParser.ReadJsonResults(prDir);

            var baseCsvResults = BenchmarkParser.ReadCsvResults(masterDir);
            var prCsvResults = BenchmarkParser.ReadCsvResults(prDir);

            var comparison = BenchmarkComparer.MatchAndCompareResults(baseJsonResults, prJsonResults, baseCsvResults, prCsvResults);

            return BenchmarkMarkdownGenerator.GetMarkdown(comparison, newBranchMarkdown, oldBranchMarkdown);
        }
    }
}
