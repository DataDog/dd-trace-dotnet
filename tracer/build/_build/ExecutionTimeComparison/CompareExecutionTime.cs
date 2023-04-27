using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Nuke.Common;
using Nuke.Common.IO;
using Perfolizer.Mathematics.SignificanceTesting;
using Perfolizer.Mathematics.Thresholds;
using Logger = Serilog.Log;

public class CompareExecutionTime
{
    private static readonly Threshold SignificantResultThreshold = Threshold.Create(ThresholdUnit.Ratio, 0.05);
    private static readonly Threshold NoiseThreshold = Threshold.Create(ThresholdUnit.Milliseconds, 5);

    public static string GetMarkdown(List<ExecutionTimeResultSource> sources)
    {
        Logger.Information("Reading execution benchmarkResults results");
        var results = sources.SelectMany(ReadJsonResults).ToList();

        // Group execution time benchmarks by Sample Name, Framework
        Logger.Information($"Found {results.Count} results: building markdown");
        var charts = results
                    .GroupBy(x => (x.TestSample, x.Framework))
                    .Select(group =>
                     {
                         var scenarios = group
                                        .Select(x => x)
                                        .OrderBy(x => x.Scenario)
                                        .GroupBy(x => x.Scenario)
                                        .Select((scenarioResults, i) => GetMermaidSection(scenarioResults.Key, scenarioResults));
                         return $"""
                        ```mermaid
                        gantt
                            title Execution time (ms) {group.Key.TestSample} ({GetName(group.Key.Framework)}) 
                            dateFormat  X
                            axisFormat %s
                            todayMarker off
                        {string.Join(Environment.NewLine, scenarios)}
                        ```
                        """;
                     });

        return GetCommentMarkdown(sources, charts);
    }
 
    static string GetMermaidSection(string scenario, IEnumerable<ExecutionTimeResult> results)
    {
        
        const decimal msToNs = 1_000_000m;
        const decimal zScore = 2.3263m;
        const string offset = "    ";
        var orderedResults = results
                            .OrderBy(x => x.Source.SourceType)
                            .ThenBy(x => x.Scenario) // baseline first
                            .ToList();

        var pairedResults = orderedResults
                           .GroupBy(x => x.Scenario)
                           .Select(@pairedScenarios =>
                            {
                                // assumes we only have master + PR for now
                                var masterValues = pairedScenarios.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.Master)?.Result.Durations;
                                var commitValues = pairedScenarios.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.CurrentCommit)?.Result.Durations;

                                if (masterValues is null || commitValues is null)
                                {
                                    return (@pairedScenarios.Key, conclusion: EquivalenceTestConclusion.Same);
                                }

                                var userThresholdResult = StatisticalTestHelper.CalculateTost(WelchTest.Instance, masterValues, commitValues, SignificantResultThreshold);
                                var conclusion = userThresholdResult.Conclusion switch
                                {
                                    EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                                    _ when StatisticalTestHelper.CalculateTost(WelchTest.Instance, masterValues, commitValues, NoiseThreshold).Conclusion == EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                                    _ => userThresholdResult.Conclusion,
                                };

                                return (@pairedScenarios.Key, conclusion);
                            })
                           .ToDictionary(x => x.Key, x => x.conclusion);

        var sb = new StringBuilder();
        sb.Append(offset).Append("section ").AppendLine(scenario);

        foreach (var result in orderedResults)
        {
            // convert everything to ms from ns
            var min = result.Result.Min / msToNs;
            var max = result.Result.Max / msToNs;
            var mean = result.Result.Mean / msToNs;
            var q05 = (result.Result.Mean - zScore * result.Result.Stdev) / msToNs;
            var q95 = (result.Result.Mean + zScore * result.Result.Stdev) / msToNs;

            var formattedMean = mean.ToString("N0");
            var format = result.Source.SourceType == ExecutionTimeSourceType.CurrentCommit
                      && scenario != "Baseline"
                      && pairedResults[scenario] == EquivalenceTestConclusion.Slower
                             ? "crit, "
                             : string.Empty;

            sb.AppendLine($"""
            {offset}{result.Source.BranchName} - mean ({formattedMean}ms)  : {format}{q05:F0}, {q95:F0}
            {offset} .   : {format}milestone, {mean:F0},
            """);
        }

        return sb.ToString();
    }

    static string GetCommentMarkdown(List<ExecutionTimeResultSource> sources, IEnumerable<string> charts)
    {
        return $"""
            ## Execution-Time Benchmarks Report :stopwatch:

            Execution-time results for samples comparing the following branches/commits:
            {string.Join('\n', GetSourceMarkdown(sources))}

            Execution-time benchmarks measure the whole time it takes to execute a program. And are intended to measure the one-off costs. Cases where the execution time results for the PR are worse than latest master results are shown in **red**. The following thresholds were used for comparing the execution times:
            * Welch test with statistical test for significance of **5%**
            * Only results indicating a difference greater than **{SignificantResultThreshold}** and **{NoiseThreshold}** are considered.


            Note that these results are based on a _single_ point-in-time result for each branch. For full results, see the [dashboard](https://ddstaging.datadoghq.com/dashboard/4qn-6fi-54p/apm-net-execution-time-benchmarks).

            Graphs show the p99 interval based on the mean and StdDev of the test run, as well as the mean value of the run (shown as a diamond below the graph).
            {string.Join('\n', charts)}
            """;

        IEnumerable<string> GetSourceMarkdown(List<ExecutionTimeResultSource> ExecutionTimeResultSources)
            => ExecutionTimeResultSources.Select(x => $"- {x.Markdown}");
    }

    private static string GetName(ExecutionTimeFramework source) => source switch
    {
        ExecutionTimeFramework.NetFramework462 => ".NET Framework 4.6.2",
        ExecutionTimeFramework.Netcoreapp31 => ".NET Core 3.1",
        ExecutionTimeFramework.Net6 => ".NET 6",
        _ => throw new NotImplementedException(),
    };

    static readonly (string Path, ExecutionTimeSample Sample, (string Filename, ExecutionTimeFramework Framework)[] TestRuns)[] ExpectedTestRuns =
    {
        ("execution_time_benchmarks_windows_x64_FakeDbCommand_1", ExecutionTimeSample.FakeDbCommand, new[] { 
            ("results_Samples.FakeDbCommand.windows.net462.json", ExecutionTimeFramework.NetFramework462),
            ("results_Samples.FakeDbCommand.windows.netcoreapp31.json", ExecutionTimeFramework.Netcoreapp31),
            ("results_Samples.FakeDbCommand.windows.net60.json", ExecutionTimeFramework.Net6),
        }),
        ("execution_time_benchmarks_windows_x64_HttpMessageHandler_1", ExecutionTimeSample.HttpMessageHandler, new[] { 
            ("results_Samples.HttpMessageHandler.windows.net462.json", ExecutionTimeFramework.NetFramework462),
            ("results_Samples.HttpMessageHandler.windows.netcoreapp31.json", ExecutionTimeFramework.Netcoreapp31),
            ("results_Samples.HttpMessageHandler.windows.net60.json", ExecutionTimeFramework.Net6),
        }),
    };

    public static List<ExecutionTimeResult> ReadJsonResults(ExecutionTimeResultSource source)
    {
        var results = new List<ExecutionTimeResult>();
        foreach (var (path, sample, testRuns) in ExpectedTestRuns)
        foreach (var (filename, framework) in testRuns)
        {
            var fileName = source.Path / path / filename;
            try
            {
                using var file = File.OpenRead(fileName);
                var node = JsonNode.Parse(file)!;
                foreach(var job in node.AsArray())
                {
                    var scenario = job["name"].ToString();
                    var durations = job["durations"].AsArray().Select(x => (double)x).ToList();
                    var result = new Result((long)job["min"], (long)job["max"], (decimal)job["mean"], (decimal)job["stdev"], durations.ToArray());
                    results.Add(new (source, sample, framework, scenario, result));
                }
            }
            catch (Exception ex)
            {
                Logger.Information($"Error reading {fileName}: {ex.Message}. Skipping");
            }
        }

        return results;
    }

    public record ExecutionTimeResult(ExecutionTimeResultSource Source, ExecutionTimeSample TestSample, ExecutionTimeFramework Framework, string Scenario, Result Result);
    public record Result(long Min, long Max, decimal Mean, decimal Stdev, double[] Durations)
    {

    }

    public enum ExecutionTimeSample
    {
        FakeDbCommand,
        HttpMessageHandler,
    }

    public enum ExecutionTimeFramework
    {
        NetFramework462,
        Netcoreapp31,
        Net6,
    }
}

public record ExecutionTimeResultSource(string BranchName, string CommitSha, ExecutionTimeSourceType SourceType, AbsolutePath Path)
{
    public string Markdown => $"[{BranchName}](https://github.com/DataDog/dd-trace-dotnet/tree/{CommitSha})";
}

public enum ExecutionTimeSourceType
{
    CurrentCommit,
    Master,
}
