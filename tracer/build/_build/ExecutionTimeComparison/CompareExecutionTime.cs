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
                                        .OrderBy(x => x.Scenario == "Baseline" ? 0 : 1 )
                                        .ThenBy(x => x.Scenario)
                                        .GroupBy(x => x.Scenario)
                                        .Select((scenarioResults, i) => GetMermaidSection(scenarioResults.Key, scenarioResults));
                         var chartTitle = $"{group.Key.TestSample} ({GetName(group.Key.Framework)})";
                         return $"""
                        <details>
                          <summary>{chartTitle}</summary>

                        ```mermaid
                        gantt
                            title Execution time (ms) {chartTitle}
                            dateFormat  x
                            axisFormat %Q
                            todayMarker off
                        {string.Join(Environment.NewLine, scenarios)}
                        ```
                        </details>
                        """;
                     });

        var comparisonTable = GetComparisonTable(results);

        return GetCommentMarkdown(sources, charts, comparisonTable);
    }
 
    static EquivalenceTestConclusion CalculateSignificance(double[] masterValues, double[] currentValues)
    {
        if (!IsValidForStatisticalTest(masterValues)|| !IsValidForStatisticalTest(currentValues))
        {
            return EquivalenceTestConclusion.Unknown;
        }

        if (!IsValidForStatisticalTest(masterValues) || !IsValidForStatisticalTest(currentValues))
        {
            return EquivalenceTestConclusion.Same;
        }

        try
        {
            if (HasZeroVariance(masterValues) || HasZeroVariance(currentValues))
            {
                // If we have zero variance, WelchTest blows up, so use a MannWhitneyU as a fallback
                var userThresholdResult = StatisticalTestHelper.CalculateTost(
                    MannWhitneyTest.Instance, masterValues, currentValues, SignificantResultThreshold);
                var conclusion = userThresholdResult.Conclusion switch
                {
                    EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ when StatisticalTestHelper.CalculateTost(MannWhitneyTest.Instance, masterValues, currentValues, NoiseThreshold).Conclusion == EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ => userThresholdResult.Conclusion,
                };

                return conclusion;
            }
            else
            {
                var userThresholdResult = StatisticalTestHelper.CalculateTost(
                    WelchTest.Instance, masterValues, currentValues, SignificantResultThreshold);
                var conclusion = userThresholdResult.Conclusion switch
                {
                    EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ when StatisticalTestHelper.CalculateTost(WelchTest.Instance, masterValues, currentValues, NoiseThreshold).Conclusion == EquivalenceTestConclusion.Same => EquivalenceTestConclusion.Same,
                    _ => userThresholdResult.Conclusion,
                };

                return conclusion;
            }
        }
        catch (Exception e)
        {
            Logger.Warning("Error calculating TOST: {Message}", e.Message);
            return EquivalenceTestConclusion.Same;
        }

        static bool IsValidForStatisticalTest(double[] values)
        {
            if (values is null || values.Length < 2)
            {
                return false;
            }

            // Check for invalid values (NaN, Infinity)
            foreach (var value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return false;
                }
            }

            return true;
        }

        static bool HasZeroVariance(double[] values)
        {
            // Check if all values are identical (zero variance)
            var first = values[0];
            foreach (var value in values)
            {
                if (Math.Abs(value - first) > 1e-10)
                {
                    return false;
                }
            }

            return true;
        }
    }

    static string GetMermaidSection(string scenario, IEnumerable<ExecutionTimeResult> results)
    {

        const decimal msToNs = 1_000_000m;
        const decimal zScore = 2.3263m;
        const string offset = "    ";
        var orderedResults = results
                            .OrderBy(x => x.Source.SourceType)
                            .ThenBy(x => x.Scenario == "Baseline" ? 0 : 1 )
                            .ThenBy(x => x.Scenario)
                            .ToList();

        var pairedResults = orderedResults
                           .GroupBy(x => x.Scenario)
                           .Select(@pairedScenarios =>
                            {
                                // assumes we only have master + PR for now
                                var masterValues = pairedScenarios.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.Master)?.Result.Durations;
                                var commitValues = pairedScenarios.FirstOrDefault(x => x.Source.SourceType == ExecutionTimeSourceType.CurrentCommit)?.Result.Durations;

                                var conclusion = CalculateSignificance(masterValues, commitValues);
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
                .ThenBy(g => g.Key.Scenario == "Baseline" ? 0 : 1)
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
                    // Use detailed metrics with pre-calculated stats
                    foreach (var metricName in keyMetrics)
                    {
                        if (!masterResult.Result.Metrics.ContainsKey(metricName) ||
                            !currentResult.Result.Metrics.ContainsKey(metricName))
                        {
                            continue;
                        }

                        var masterStats = masterResult.Result.Metrics[metricName];
                        var currentStats = currentResult.Result.Metrics[metricName];

                        // Check if we have the required stats
                        if (!masterStats.ContainsKey("mean") || !masterStats.ContainsKey("std_err") ||
                            !currentStats.ContainsKey("mean") || !currentStats.ContainsKey("std_err"))
                        {
                            continue;
                        }

                        // Calculate 95% CI from standard error: mean ± 1.96 * std_err
                        const double z95 = 1.96;
                        var masterMean = masterStats["mean"];
                        var masterStdErr = masterStats["std_err"];
                        var masterCiLower = masterMean - (z95 * masterStdErr);
                        var masterCiUpper = masterMean + (z95 * masterStdErr);

                        var currentMean = currentStats["mean"];
                        var currentStdErr = currentStats["std_err"];
                        var currentCiLower = currentMean - (z95 * currentStdErr);
                        var currentCiUpper = currentMean + (z95 * currentStdErr);

                        // Calculate significance using raw metricsData arrays
                        var masterMetricData = masterResult.Result.MetricsData.GetValueOrDefault(metricName);
                        var currentMetricData = currentResult.Result.MetricsData.GetValueOrDefault(metricName);
                        var significance = CalculateSignificance(masterMetricData, currentMetricData);

                        var (rowHtml, isRegression) = FormatMetricRowFromStats(
                            metricName,
                            masterStats: (masterMean, masterCiLower, masterCiUpper),
                            currentStats: (currentMean, currentCiLower, currentCiUpper),
                            convertFromNs: false,
                            significance: significance);
                        tableRows.Add((rowHtml, isRegression));
                    }
                }
                else if (hasDuration)
                {
                    // Fall back to duration for .NET Framework - use pre-calculated stats from Result
                    var significance = CalculateSignificance(masterResult.Result.Durations, currentResult.Result.Durations);
                    var (rowHtml, isRegression) = FormatMetricRowFromStats(
                        "duration",
                        masterStats: ((double)masterResult.Result.Median, (double)masterResult.Result.LowerCi95Bound, (double)masterResult.Result.UpperCi95Bound),
                        currentStats: ((double)currentResult.Result.Median, (double)currentResult.Result.LowerCi95Bound, (double)currentResult.Result.UpperCi95Bound),
                        convertFromNs: true,
                        significance: significance);
                    tableRows.Add((rowHtml, isRegression));
                }

                if (tableRows.Count == 0)
                {
                    continue;
                }

                // Add section header row
                detailsTableRows.AppendLine($"    <tr><th colspan=\"5\" align=\"left\">{scenarioName}</th></tr>");

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
                    regressionsTableRows.AppendLine($"    <tr><th colspan=\"5\" align=\"left\">{scenarioName}</th></tr>");
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
                detailsOutput.AppendLine("      <th>Master (Mean ± 95% CI)</th>");
                detailsOutput.AppendLine("      <th>Current (Mean ± 95% CI)</th>");
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
                regressionsOutput.AppendLine("      <th>Master (Mean ± 95% CI)</th>");
                regressionsOutput.AppendLine("      <th>Current (Mean ± 95% CI)</th>");
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
            finalOutput.AppendLine("### ⚠️ Potential regressions detected");
            finalOutput.AppendLine();
            finalOutput.Append(regressionsOutput);
        }
        else
        {
            finalOutput.AppendLine("✅ No regressions detected - check the details below");
            finalOutput.AppendLine();

        }

        finalOutput.AppendLine("<details>");
        finalOutput.AppendLine("  <summary>Full Metrics Comparison</summary>");
        finalOutput.AppendLine();
        finalOutput.Append(detailsOutput);
        finalOutput.AppendLine("</details>");

        return finalOutput.ToString();
    }

    static (string html, bool isRegression) FormatMetricRowFromStats(
        string metricName,
        (double Mean, double Ci95Lower, double Ci95Upper) masterStats,
        (double Mean, double Ci95Lower, double Ci95Upper) currentStats,
        bool convertFromNs,
        EquivalenceTestConclusion significance)
    {
        // Convert from nanoseconds to milliseconds if needed
        const double nsToMs = 1_000_000.0;
        if (convertFromNs)
        {
            masterStats = (masterStats.Mean / nsToMs, masterStats.Ci95Lower / nsToMs, masterStats.Ci95Upper / nsToMs);
            currentStats = (currentStats.Mean / nsToMs, currentStats.Ci95Lower / nsToMs, currentStats.Ci95Upper / nsToMs);
        }

        var changePct = masterStats.Mean != 0
            ? ((currentStats.Mean - masterStats.Mean) / masterStats.Mean) * 100
            : 0;

        var (status, isRegression) = GetStatusInfo(significance, metricName);
        var changeText = changePct >= 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%";

        // Add upward arrow for increases to make them easier to scan
        var statusWithArrow = changePct > 0 ? $"{status}⬆️" : status;

        var masterText = FormatMetricValue(metricName, masterStats.Mean, masterStats.Ci95Lower, masterStats.Ci95Upper);
        var currentText = FormatMetricValue(metricName, currentStats.Mean, currentStats.Ci95Lower, currentStats.Ci95Upper);

        var rowHtml = $"    <tr><td>{GetMetricDisplayName(metricName)}</td><td>{masterText}</td><td>{currentText}</td><td>{changeText}</td><td style=\"text-align: right;\">{statusWithArrow}</td></tr>";

        return (rowHtml, isRegression);
    }

    static string FormatMetricValue(string metricName, double mean, double ci95Lower, double ci95Upper)
    {
        if (metricName.Contains("mem.committed"))
        {
            // Format as MB
            return $"{mean / 1_024_024:F2} ± ({ci95Lower / 1_024_024:F2} - {ci95Upper / 1_024_024:F2}) MB";
        }
        else if (metricName.Contains("_ms") || metricName == "duration")
        {
            // Format as milliseconds
            return $"{mean:F2} ± ({ci95Lower:F2} - {ci95Upper:F2}) ms";
        }
        else
        {
            // Format as integer
            return $"{mean:F0} ± ({ci95Lower:F0} - {ci95Upper:F0})";
        }
    }

    static string GetMetricDisplayName(string metricName) => metricName;

    static (string emoji, bool isRegression) GetStatusInfo(EquivalenceTestConclusion significance, string metricName)
    {
        // Use statistical test result to determine status
        if (significance == EquivalenceTestConclusion.Same)
        {
            return ("✅", false); // No significant change
        }

        // For most metrics, increase is worse (slower, more exceptions, more memory)
        return significance == EquivalenceTestConclusion.Slower
            ? ("❌", true) // Regression (slower/higher is worse)
            : ("✅", false); // Improvement (faster/lower is better)
    }

    static string GetCommentMarkdown(List<ExecutionTimeResultSource> sources, IEnumerable<string> charts, string comparisonTable)
    {
        return $$"""
            ## Execution-Time Benchmarks Report :stopwatch:

            Execution-time results for samples comparing {{string.Join(" and ", sources.Select(x => x.Markdown))}}.

            {{comparisonTable}}

            <details>
              <summary>Comparison explanation</summary>
              <p>
              Execution-time benchmarks measure the whole time it takes to execute a program, and are intended to measure the one-off costs.
              Cases where the execution time results for the PR are worse than latest master results are highlighted in **red**.
              The following thresholds were used for comparing the execution times:</p>
              <ul>
                <li>Welch test with statistical test for significance of <strong>5%</strong></li>
                <li>Only results indicating a difference greater than <strong>{{SignificantResultThreshold}}</strong> and <strong>{{NoiseThreshold}}</strong> are considered.</li>
              </ul>
              <p>
                Note that these results are based on a <em>single</em> point-in-time result for each branch.
                For full results, see the <a href="https://app.datadoghq.com/dashboard/gf2-auj-wve">dashboard</a>.
              </p>
              <p>
                Graphs show the p99 interval based on the mean and StdDev of the test run, as well as the mean value of the run (shown as a diamond below the graph).
              </p>
            </details>

            <details>
            <summary>Duration charts</summary>
            {{string.Join('\n', charts)}}
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

                    // Parse metrics with suffixed stats (e.g., "process.time_to_start_ms.mean", "process.time_to_start_ms.std_err")
                    var metrics = new Dictionary<string, Dictionary<string, double>>();
                    if (job["metrics"] is JsonObject metricsObj)
                    {
                        foreach (var metric in metricsObj)
                        {
                            var fullKey = metric.Key;
                            var lastDotIndex = fullKey.LastIndexOf('.');

                            if (lastDotIndex > 0)
                            {
                                // Split into base metric name and stat suffix
                                var baseMetric = fullKey.Substring(0, lastDotIndex);
                                var statSuffix = fullKey.Substring(lastDotIndex + 1);

                                if (!metrics.ContainsKey(baseMetric))
                                {
                                    metrics[baseMetric] = new Dictionary<string, double>();
                                }

                                metrics[baseMetric][statSuffix] = (double)metric.Value;
                            }
                        }
                    }

                    // Parse raw metricsData arrays (e.g., "metricsData"."runtime.dotnet.threads.count": [3, 4, 5, ...])
                    var metricsData = new Dictionary<string, double[]>();
                    if (job["metricsData"] is JsonObject metricsDataObj)
                    {
                        foreach (var metricData in metricsDataObj)
                        {
                            if (metricData.Value is JsonArray dataArray)
                            {
                                metricsData[metricData.Key] = dataArray.Select(x => (double)x).ToArray();
                            }
                        }
                    }

                    var result = new Result((long) job["min"],
                        Max: (long) job["max"],
                        Mean: (decimal) job["mean"],
                        Median: (decimal) job["median"],
                        LowerCi95Bound: (decimal) job["ci95"]?[0],
                        UpperCi95Bound: (decimal) job["ci95"]?[1],
                        (decimal) job["stdev"],
                        durations.ToArray(),
                        metrics,
                        metricsData);
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
    public record Result(long Min, long Max, decimal Mean, decimal Median, decimal LowerCi95Bound, decimal UpperCi95Bound, decimal Stdev, double[] Durations, Dictionary<string, Dictionary<string, double>> Metrics, Dictionary<string, double[]> MetricsData)
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
