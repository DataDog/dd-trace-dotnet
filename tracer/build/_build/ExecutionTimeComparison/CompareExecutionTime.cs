using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                        <code lang="mermaid"><pre lang="mermaid">
                        gantt
                            title Execution time (ms) {group.Key.TestSample} ({GetName(group.Key.Framework)})
                            dateFormat  X
                            axisFormat %s
                            todayMarker off
                        {string.Join(Environment.NewLine, scenarios)}
                        </pre></code>
                        """;
                     });

        var comparisonTable = GetComparisonTable(results);

        return GetCommentMarkdown(sources, charts, comparisonTable);
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

                                try
                                {
                                    var userThresholdResult = StatisticalTestHelper.CalculateTost(WelchTest.Instance, masterValues, commitValues, SignificantResultThreshold);
                                    var conclusion = userThresholdResult.Conclusion switch
                                    {
                                        EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                                        _ when StatisticalTestHelper.CalculateTost(WelchTest.Instance, masterValues, commitValues, NoiseThreshold).Conclusion == EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                                        _ => userThresholdResult.Conclusion,
                                    };

                                    return (@pairedScenarios.Key, conclusion);
                                }
                                catch (Exception e)
                                {
                                    Logger.Warning("Error calculating TOST for {Scenario}: {Message}", @pairedScenarios.Key, e.Message);
                                    return (@pairedScenarios.Key, conclusion: EquivalenceTestConclusion.Same);
                                }
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

            // Ensure q05 is not negative (which would cause mermaid to start from 0)
            q05 = Math.Max(0, q05);

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

    static string GetComparisonTable(List<ExecutionTimeResult> results)
    {
        // Key metrics to compare
        var keyMetrics = new[]
        {
            "process.internal_duration_ms",
            "process.time_to_main_ms",
            "runtime.dotnet.exceptions.count",
            "runtime.dotnet.mem.committed",
            "runtime.dotnet.threads.count"
        };

        var regressionsOutput = new StringBuilder();
        var detailsOutput = new StringBuilder();
        var hasRegressions = false;

        // Group by Sample first, then by Framework and Scenario
        var sampleGroups = results
            .GroupBy(x => x.TestSample)
            .OrderBy(g => g.Key);

        foreach (var sampleGroup in sampleGroups)
        {
            var sampleName = sampleGroup.Key.ToString();
            var regressionsTableRows = new StringBuilder();
            var detailsTableRows = new StringBuilder();
            var sampleHasRegressions = false;

            var grouped = sampleGroup
                .GroupBy(x => (x.Framework, x.Scenario))
                .OrderBy(g => g.Key.Framework)
                .ThenBy(g => g.Key.Scenario);

            foreach (var group in grouped)
            {
                var masterResult = group.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.Master);
                var currentResult = group.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.CurrentCommit);

                if (masterResult == null || currentResult == null)
                {
                    continue;
                }

                var scenarioName = $"{GetName(group.Key.Framework)} - {group.Key.Scenario}";
                var tableRows = new List<(string html, bool isRegression)>();

                // Check if we have detailed metrics or just duration
                var hasDuration = masterResult.Result.Durations.Length > 0 && currentResult.Result.Durations.Length > 0;
                var hasDetailedMetrics = masterResult.Result.Metrics.Any();

                if (hasDetailedMetrics)
                {
                    // Use detailed metrics
                    foreach (var metricName in keyMetrics)
                    {
                        if (!masterResult.Result.Metrics.ContainsKey(metricName) ||
                            !currentResult.Result.Metrics.ContainsKey(metricName))
                        {
                            continue;
                        }

                        var masterValues = masterResult.Result.Metrics[metricName];
                        var currentValues = currentResult.Result.Metrics[metricName];

                        if (masterValues.Length == 0 || currentValues.Length == 0)
                        {
                            continue;
                        }

                        var (rowHtml, isRegression) = FormatMetricRow(metricName, masterValues, currentValues, convertFromNs: false);
                        tableRows.Add((rowHtml, isRegression));
                    }
                }
                else if (hasDuration)
                {
                    // Fall back to duration for .NET Framework - values are in nanoseconds
                    var (rowHtml, isRegression) = FormatMetricRow("duration", masterResult.Result.Durations, currentResult.Result.Durations, convertFromNs: true);
                    tableRows.Add((rowHtml, isRegression));
                }

                if (tableRows.Count == 0)
                {
                    continue;
                }

                // Add section header row
                detailsTableRows.AppendLine($"    <tr><th colspan=\"5\">{scenarioName}</th></tr>");

                var hasAnyRegression = false;
                var regressionRows = new StringBuilder();

                foreach (var (rowHtml, isRegression) in tableRows)
                {
                    detailsTableRows.AppendLine(rowHtml);
                    if (isRegression)
                    {
                        hasAnyRegression = true;
                        regressionRows.AppendLine(rowHtml);
                    }
                }

                // If there are regressions, also add to the regressions table
                if (hasAnyRegression)
                {
                    sampleHasRegressions = true;
                    regressionsTableRows.AppendLine($"    <tr><th colspan=\"5\">{scenarioName}</th></tr>");
                    regressionsTableRows.Append(regressionRows);
                }
            }

            // Build table for this sample in details
            if (detailsTableRows.Length > 0)
            {
                detailsOutput.AppendLine($"<h4>{sampleName}</h4>");
                detailsOutput.AppendLine("<table>");
                detailsOutput.AppendLine("  <thead>");
                detailsOutput.AppendLine("    <tr>");
                detailsOutput.AppendLine("      <th>Metric</th>");
                detailsOutput.AppendLine("      <th>Master (Median ± 95% CI)</th>");
                detailsOutput.AppendLine("      <th>Current (Median ± 95% CI)</th>");
                detailsOutput.AppendLine("      <th>Change</th>");
                detailsOutput.AppendLine("      <th>Status</th>");
                detailsOutput.AppendLine("    </tr>");
                detailsOutput.AppendLine("  </thead>");
                detailsOutput.AppendLine("  <tbody>");
                detailsOutput.Append(detailsTableRows);
                detailsOutput.AppendLine("  </tbody>");
                detailsOutput.AppendLine("</table>");
                detailsOutput.AppendLine();
            }

            // Build table for this sample in regressions
            if (sampleHasRegressions)
            {
                hasRegressions = true;
                regressionsOutput.AppendLine($"<h4>{sampleName}</h4>");
                regressionsOutput.AppendLine("<table>");
                regressionsOutput.AppendLine("  <thead>");
                regressionsOutput.AppendLine("    <tr>");
                regressionsOutput.AppendLine("      <th>Metric</th>");
                regressionsOutput.AppendLine("      <th>Master (Median ± 95% CI)</th>");
                regressionsOutput.AppendLine("      <th>Current (Median ± 95% CI)</th>");
                regressionsOutput.AppendLine("      <th>Change</th>");
                regressionsOutput.AppendLine("      <th>Status</th>");
                regressionsOutput.AppendLine("    </tr>");
                regressionsOutput.AppendLine("  </thead>");
                regressionsOutput.AppendLine("  <tbody>");
                regressionsOutput.Append(regressionsTableRows);
                regressionsOutput.AppendLine("  </tbody>");
                regressionsOutput.AppendLine("</table>");
                regressionsOutput.AppendLine();
            }
        }

        var finalOutput = new StringBuilder();

        if (hasRegressions)
        {
            finalOutput.AppendLine("### ⚠️ Potential Regressions Detected");
            finalOutput.AppendLine();
            finalOutput.Append(regressionsOutput);
        }

        finalOutput.AppendLine("<details>");
        finalOutput.AppendLine("  <summary>Full Metrics Comparison</summary>");
        finalOutput.AppendLine();
        finalOutput.Append(detailsOutput);
        finalOutput.AppendLine("</details>");

        return finalOutput.ToString();
    }

    static (string html, bool isRegression) FormatMetricRow(string metricName, double[] masterValues, double[] currentValues, bool convertFromNs)
    {
        var masterStats = CalculateStats(masterValues, convertFromNs);
        var currentStats = CalculateStats(currentValues, convertFromNs);

        var changePct = masterStats.Median != 0
            ? ((currentStats.Median - masterStats.Median) / masterStats.Median) * 100
            : 0;

        var (status, isRegression) = GetStatusInfo(changePct, metricName);
        var changeText = changePct >= 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%";

        var masterText = FormatMetricValue(metricName, masterStats.Median, masterStats.Ci95Lower, masterStats.Ci95Upper);
        var currentText = FormatMetricValue(metricName, currentStats.Median, currentStats.Ci95Lower, currentStats.Ci95Upper);

        var rowHtml = $"    <tr><td>{GetMetricDisplayName(metricName)}</td><td>{masterText}</td><td>{currentText}</td><td>{changeText}</td><td>{status}</td></tr>";

        return (rowHtml, isRegression);
    }

    static (double Median, double Ci95Lower, double Ci95Upper) CalculateStats(double[] values, bool convertFromNs = false)
    {
        if (values.Length == 0)
        {
            return (0, 0, 0);
        }

        // Convert from nanoseconds to milliseconds if needed
        const double nsToMs = 1_000_000.0;
        var convertedValues = convertFromNs
            ? values.Select(v => v / nsToMs).ToArray()
            : values;

        var sorted = convertedValues.OrderBy(x => x).ToArray();
        var median = sorted.Length % 2 == 0
            ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2
            : sorted[sorted.Length / 2];

        // Calculate 95% CI using percentiles (2.5th and 97.5th percentiles)
        var lowerIndex = (int)Math.Floor(sorted.Length * 0.025);
        var upperIndex = (int)Math.Ceiling(sorted.Length * 0.975) - 1;
        upperIndex = Math.Min(upperIndex, sorted.Length - 1);

        var ci95Lower = sorted[lowerIndex];
        var ci95Upper = sorted[upperIndex];

        return (median, ci95Lower, ci95Upper);
    }

    static string FormatMetricValue(string metricName, double median, double ci95Lower, double ci95Upper)
    {
        if (metricName.Contains("mem.committed"))
        {
            // Format as MB
            return $"{median / 1_024_024:F2} ± ({ci95Lower / 1_024_024:F2} - {ci95Upper / 1_024_024:F2}) MB";
        }
        else if (metricName.Contains("_ms") || metricName == "duration")
        {
            // Format as milliseconds
            return $"{median:F2} ± ({ci95Lower:F2} - {ci95Upper:F2}) ms";
        }
        else
        {
            // Format as integer
            return $"{median:F0} ± ({ci95Lower:F0} - {ci95Upper:F0})";
        }
    }

    static string GetMetricDisplayName(string metricName) => metricName;

    static (string emoji, bool isRegression) GetStatusInfo(double changePct, string metricName)
    {
        // For exceptions and most metrics, lower is better
        // For thread count, changes might be neutral
        var threshold = 5.0; // 5% threshold for significant change

        if (Math.Abs(changePct) < threshold)
        {
            return ("✅", false); // No significant change
        }

        // For most metrics, increase is worse
        if (metricName.Contains("exceptions") || metricName.Contains("duration") ||
            metricName.Contains("time_to") || metricName.Contains("mem") || metricName == "duration")
        {
            if (changePct > 0)
            {
                return ("❌", true); // Regression
            }
            else
            {
                return ("✅", false); // Improvement
            }
        }

        // For thread count, treat as neutral
        return ("⚠️", false);
    }

    static string GetCommentMarkdown(List<ExecutionTimeResultSource> sources, IEnumerable<string> charts, string comparisonTable)
    {
        return $"""
            ## Execution-Time Benchmarks Report :stopwatch:

            Execution-time results for samples comparing
            {string.Join(" and ", sources.Select(x => x.Markdown))}.

            {comparisonTable}

            <details>
              <summary>Comparison explanation</summary>
              <p>
              Execution-time benchmarks measure the whole time it takes to execute a program, and are intended to measure the one-off costs.
              Cases where the execution time results for the PR are worse than latest master results are highlighted in **red**.
              The following thresholds were used for comparing the execution times:</p>
              <ul>
                <li>Welch test with statistical test for significance of <strong>5%</strong></li>
                <li>Only results indicating a difference greater than <strong>{SignificantResultThreshold}</strong> and <strong>{NoiseThreshold}</strong> are considered.</li>
              </ul>
              <p>
                Note that these results are based on a <em>single</em> point-in-time result for each branch.
                For full results, see the <a href="https://ddstaging.datadoghq.com/dashboard/4qn-6fi-54p/apm-net-execution-time-benchmarks">dashboard</a>.
              </p>
              <p>
                Graphs show the p99 interval based on the mean and StdDev of the test run, as well as the mean value of the run (shown as a diamond below the graph).
              </p>
            </details>

            <details>
              <summary>Execution-time charts</summary>

            {string.Join('\n', charts)}
            </details>
            """;
    }

    private static string GetName(ExecutionTimeFramework source) => source switch
    {
        ExecutionTimeFramework.NetFramework => ".NET Framework 4.8",
        ExecutionTimeFramework.Netcoreapp31 => ".NET Core 3.1",
        ExecutionTimeFramework.Net6 => ".NET 6",
        ExecutionTimeFramework.Net8 => ".NET 8",
        _ => throw new NotImplementedException(),
    };

    static readonly (string Path, ExecutionTimeSample Sample, (string Filename, ExecutionTimeFramework Framework)[] TestRuns)[] ExpectedTestRuns =
    {
        ("execution_time_benchmarks_windows_x64_FakeDbCommand_1", ExecutionTimeSample.FakeDbCommand, new[] { 
            ("results_Samples.FakeDbCommand.windows.net48.json", ExecutionTimeFramework.NetFramework),
            ("results_Samples.FakeDbCommand.windows.netcoreapp31.json", ExecutionTimeFramework.Netcoreapp31),
            ("results_Samples.FakeDbCommand.windows.net60.json", ExecutionTimeFramework.Net6),
            ("results_Samples.FakeDbCommand.windows.net80.json", ExecutionTimeFramework.Net8),
        }),
        ("execution_time_benchmarks_windows_x64_HttpMessageHandler_1", ExecutionTimeSample.HttpMessageHandler, new[] { 
            ("results_Samples.HttpMessageHandler.windows.net48.json", ExecutionTimeFramework.NetFramework),
            ("results_Samples.HttpMessageHandler.windows.netcoreapp31.json", ExecutionTimeFramework.Netcoreapp31),
            ("results_Samples.HttpMessageHandler.windows.net60.json", ExecutionTimeFramework.Net6),
            ("results_Samples.HttpMessageHandler.windows.net80.json", ExecutionTimeFramework.Net8),
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

                    // Parse metrics from the data array
                    var metricsDict = new Dictionary<string, List<double>>();
                    if (job["data"] is JsonArray dataArray)
                    {
                        foreach (var dataPoint in dataArray)
                        {
                            if (dataPoint["metrics"] is JsonObject metricsObj)
                            {
                                foreach (var metric in metricsObj)
                                {
                                    if (!metricsDict.ContainsKey(metric.Key))
                                    {
                                        metricsDict[metric.Key] = new List<double>();
                                    }
                                    metricsDict[metric.Key].Add((double)metric.Value);
                                }
                            }
                        }
                    }

                    var metrics = metricsDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                    var result = new Result((long) job["min"],
                        Max: (long) job["max"],
                        Mean: (decimal) job["mean"],
                        Median: (decimal) job["median"],
                        LowerCi95Bound: (decimal) job["ci95"]?[0],
                        UpperCi95Bound: (decimal) job["ci95"]?[1],
                        (decimal) job["stdev"],
                        durations.ToArray(),
                        metrics);
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
    public record Result(long Min, long Max, decimal Mean, decimal Median, decimal LowerCi95Bound, decimal UpperCi95Bound, decimal Stdev, double[] Durations, Dictionary<string, double[]> Metrics)
    {

    }

    public enum ExecutionTimeSample
    {
        FakeDbCommand,
        HttpMessageHandler,
    }

    public enum ExecutionTimeFramework
    {
        NetFramework,
        Netcoreapp31,
        Net6,
        Net8,
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
