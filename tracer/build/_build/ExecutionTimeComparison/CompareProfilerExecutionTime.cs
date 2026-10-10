using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Perfolizer.Mathematics.SignificanceTesting;
using Logger = Serilog.Log;

/// <summary>
/// Builds the comparison report for the Continuous Profiler execution-time benchmarks
/// (the <c>profiler_execution_benchmarks</c> stage), comparing the current commit against master.
/// </summary>
public class CompareProfilerExecutionTime
{
    public const string CommentTitle = "## Profiler Execution-Time Benchmarks Report";

    const string DurationKey = "duration";

    // Must match the testName values of the profiler_execution_benchmarks matrix in ultimate-pipeline.yml
    static readonly string[] TestNames = { "cpu-walltime", "exceptions", "contention", "allocations", "liveheap", "garbagecollections" };
    static readonly string[] OperatingSystems = { "windows", "linux" };

    // SignificanceScale converts the values to nanoseconds-equivalent so that the
    // CompareExecutionTime.NoiseThreshold (5ms) is meaningful (for private bytes, it acts as a ~5MB noise floor)
    static readonly MetricInfo[] Metrics =
    {
        new(DurationKey, "Duration", SignificanceScale: 1, FormatValue: v => $"{v / 1_000_000:N0} ms"),
        new("runtime.process.processor_time", "CPU time", SignificanceScale: 1_000_000, FormatValue: v => $"{v:N0} ms"),
        new("runtime.process.private_bytes", "Private bytes", SignificanceScale: 1, FormatValue: v => $"{v / 1024 / 1024:N1} MB"),
    };

    // The Windows artifacts contain both the x64 and x86 runs, despite the name
    public static IEnumerable<string> ArtifactNames
        => from os in OperatingSystems
           from test in TestNames
           select GetArtifactName(os, test);

    public static string ReferenceArtifactName => GetArtifactName(OperatingSystems[0], TestNames[0]);

    static string GetArtifactName(string os, string test) => $"profiler_execution_time_benchmarks_{os}_x64_{test}_1";

    public static string GetMarkdown(List<ExecutionTimeResultSource> sources)
    {
        var comparisons = Compare(ReadAllResults(sources));

        return $$"""
            <h1>Profiler Execution-Time Benchmarks Report ⏱️</h1>

            Execution-time results for the Continuous Profiler samples comparing {{GetSourcesMarkdown(sources)}}.

            <h2 id="comparison-results">Comparison Results</h2>

            {{GetRegressionsMarkdown(comparisons, header: "⚠️ Potential regressions detected")}}

            <h3 id="full-comparison">Full Comparison</h3>

            {{GetDetailedTables(comparisons, regressionsOnly: false)}}

            <details>
              <summary><span id="comparison-explanation">Comparison Explanation</span></summary>
              <p>
              Execution-time benchmarks run the profiler sample applications with different profiler configurations, and measure the whole execution time, the CPU time and the private bytes of the process.
              Each scenario of the PR is compared with the same scenario of the latest master build that has results.
              The following thresholds were used for the comparison:</p>
              <ul>
                <li>Welch test with statistical test for significance of <strong>5%</strong></li>
                <li>Only results indicating a difference greater than <strong>{{CompareExecutionTime.SignificantResultThreshold}}</strong> and <strong>{{CompareExecutionTime.NoiseThreshold}}</strong> (~5 MB for private bytes) are considered.</li>
                <li><code>Baseline_*</code> scenarios run without the profiler: a change there indicates noise on the benchmark machine, so it is never reported as a regression.</li>
              </ul>
              <p>
                The <em>Overhead</em> column is the difference with the <code>Baseline_*</code> scenario of the same run.
                Note that these results are based on a <em>single</em> point-in-time result for each branch.
              </p>
            </details>

            ---

            <h3 id="duration-charts">Duration Charts</h3>

            {{GetCharts(comparisons)}}
            """;
    }

    /// <summary>
    /// Returns a concise summary for the PR comment: the regressions, and a compact table with all
    /// the results in a collapsed section. The caller is expected to append a link to the full report.
    /// </summary>
    public static string GetCommentSummary(List<ExecutionTimeResultSource> sources)
    {
        var comparisons = Compare(ReadAllResults(sources));

        if (!comparisons.Any(x => x.MasterResult is not null))
        {
            return $"""
                {CommentTitle} ⏱️

                No profiler execution-time results could be compared for {GetSourcesMarkdown(sources)}.
                """;
        }

        return $"""
            {CommentTitle} ⏱️

            Execution-time results for the Continuous Profiler samples comparing {GetSourcesMarkdown(sources)}.

            {GetRegressionsMarkdown(comparisons, header: "### ⚠️ Potential regressions detected")}

            <details>
            <summary>All results (current value and change vs master)</summary>

            {GetCompactTables(comparisons)}
            </details>
            """;
    }

    static string GetSourcesMarkdown(List<ExecutionTimeResultSource> sources)
        => string.Join(" and ", sources.Select(x => x.Markdown));

    static string GetRegressionsMarkdown(List<ScenarioComparison> comparisons, string header)
        => comparisons.Any(x => x.Metrics.Any(m => m.IsRegression))
               ? header + "\n\n" + GetDetailedTables(comparisons, regressionsOnly: true)
               : "✅ No regressions detected";

    static List<ProfilerResult> ReadAllResults(List<ExecutionTimeResultSource> sources)
    {
        Logger.Information("Reading profiler execution benchmark results");
        var results = sources.SelectMany(ReadJsonResults).ToList();
        Logger.Information("Found {Count} profiler execution benchmark results", results.Count);
        return results;
    }

    static List<ProfilerResult> ReadJsonResults(ExecutionTimeResultSource source)
    {
        // Keyed by (test, platform, scenario), later files override earlier ones (e.g. after a retry)
        var results = new Dictionary<(string Test, string Platform, string Scenario), ProfilerResult>();

        foreach (var os in OperatingSystems)
        foreach (var test in TestNames)
        {
            var directory = source.Path / GetArtifactName(os, test);
            if (!Directory.Exists(directory))
            {
                Logger.Information("No results found in {Directory}. Skipping", directory);
                continue;
            }

            var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
                                 .OrderBy(File.GetLastWriteTimeUtc);

            foreach (var fileName in files)
            {
                try
                {
                    using var file = File.OpenRead(fileName);
                    var jobs = JsonNode.Parse(file)!.AsArray();
                    for (var i = 0; i < jobs.Count; i++)
                    {
                        var result = ParseJob(source, os, test, scenarioOrder: i, jobs[i]!);
                        if (result is not null)
                        {
                            results[(test, result.Platform, result.Scenario)] = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Information("Error reading {FileName}: {Message}. Skipping", fileName, ex.Message);
                }
            }
        }

        return results.Values.ToList();
    }

    static ProfilerResult ParseJob(ExecutionTimeResultSource source, string os, string test, int scenarioOrder, JsonNode job)
    {
        var durations = ToArray(job["durations"]);
        if (durations.Length == 0)
        {
            return null;
        }

        var values = new Dictionary<string, MetricValue>();

        var mean = (double)job["mean"]!;
        var ci95 = job["ci95"]?.AsArray();
        values[DurationKey] = ci95 is { Count: 2 }
                                  ? new MetricValue(mean, (double)ci95[0]!, (double)ci95[1]!, durations)
                                  : new MetricValue(mean, mean, mean, durations);

        foreach (var metric in Metrics.Where(x => x.Key != DurationKey))
        {
            var metricMean = (double?)job["metrics"]?[$"{metric.Key}.mean"];
            var metricData = ToArray(job["metricsData"]?[metric.Key]);
            if (metricMean is null || metricData.Length == 0)
            {
                continue;
            }

            // 95% CI from the standard error: mean ± 1.96 * std_err
            var stdErr = (double?)job["metrics"]?[$"{metric.Key}.std_err"] ?? 0;
            values[metric.Key] = new MetricValue(metricMean.Value, metricMean.Value - (1.96 * stdErr), metricMean.Value + (1.96 * stdErr), metricData);
        }

        var arch = job["tags"]?["runtime.architecture"]?.ToString() ?? "x64";
        return new ProfilerResult(
            source,
            test,
            Platform: $"{GetOsName(os)} {arch}",
            Scenario: job["name"]!.ToString(),
            scenarioOrder,
            DurationStdev: (double?)job["stdev"] ?? 0,
            values);

        static double[] ToArray(JsonNode node) => node?.AsArray().Select(x => (double)x).ToArray() ?? Array.Empty<double>();
    }

    static List<ScenarioComparison> Compare(List<ProfilerResult> results)
    {
        var comparisons = new List<ScenarioComparison>();

        var groups = results
                    .GroupBy(x => (x.Test, x.Platform))
                    .OrderBy(g => Array.IndexOf(TestNames, g.Key.Test))
                    .ThenBy(g => g.Key.Platform.StartsWith("Windows") ? 0 : 1)
                    .ThenBy(g => g.Key.Platform);

        foreach (var group in groups)
        {
            var master = group.Where(x => x.Source.SourceType == ExecutionTimeSourceType.Master).ToDictionary(x => x.Scenario);
            var current = group.Where(x => x.Source.SourceType == ExecutionTimeSourceType.CurrentCommit).ToList();

            var masterBaseline = master.Values.FirstOrDefault(x => IsBaseline(x.Scenario));
            var currentBaseline = current.FirstOrDefault(x => IsBaseline(x.Scenario));

            foreach (var currentResult in current.OrderBy(x => x.ScenarioOrder))
            {
                master.TryGetValue(currentResult.Scenario, out var masterResult);
                var isBaseline = IsBaseline(currentResult.Scenario);

                var metrics = new List<MetricComparison>();
                foreach (var metric in Metrics)
                {
                    if (!currentResult.Values.TryGetValue(metric.Key, out var currentValue))
                    {
                        continue;
                    }

                    MetricValue masterValue = null;
                    masterResult?.Values.TryGetValue(metric.Key, out masterValue);

                    var slower = masterValue is not null
                              && CompareExecutionTime.CalculateSignificance(
                                     masterValue.Data.Select(x => x * metric.SignificanceScale).ToArray(),
                                     currentValue.Data.Select(x => x * metric.SignificanceScale).ToArray()) == EquivalenceTestConclusion.Slower;

                    metrics.Add(new MetricComparison(
                        metric,
                        masterValue,
                        currentValue,
                        MasterOverhead: isBaseline ? null : GetOverhead(masterValue, masterBaseline, metric),
                        CurrentOverhead: isBaseline ? null : GetOverhead(currentValue, currentBaseline, metric),
                        IsSlower: slower,
                        IsRegression: slower && !isBaseline));
                }

                comparisons.Add(new ScenarioComparison(group.Key.Test, group.Key.Platform, currentResult.Scenario, masterResult, currentResult, metrics));
            }
        }

        return comparisons;

        static double? GetOverhead(MetricValue value, ProfilerResult baseline, MetricInfo metric)
            => value is not null
            && baseline is not null
            && baseline.Values.TryGetValue(metric.Key, out var baselineValue)
            && baselineValue.Mean != 0
                   ? (value.Mean - baselineValue.Mean) / baselineValue.Mean * 100
                   : null;
    }

    static bool IsBaseline(string scenario) => scenario.StartsWith("Baseline", StringComparison.OrdinalIgnoreCase);

    static string GetDetailedTables(List<ScenarioComparison> comparisons, bool regressionsOnly)
    {
        var sb = new StringBuilder();
        foreach (var testGroup in comparisons.GroupBy(x => x.Test))
        {
            var rows = new StringBuilder();
            foreach (var scenario in testGroup)
            foreach (var metric in scenario.Metrics.Where(x => !regressionsOnly || x.IsRegression))
            {
                var overhead = metric.CurrentOverhead is null && metric.MasterOverhead is null
                                   ? "-"
                                   : $"{FormatPercent(metric.MasterOverhead)} → {FormatPercent(metric.CurrentOverhead)}";

                rows.AppendLine($"| {scenario.Platform} | {scenario.Scenario} | {metric.Info.DisplayName} | {FormatWithCi(metric.Info, metric.Master)} | {FormatWithCi(metric.Info, metric.Current)} | {FormatPercent(metric.Change)} | {overhead} | {GetStatus(metric)} |");
            }

            if (rows.Length == 0)
            {
                continue;
            }

            sb.AppendLine($"#### {testGroup.Key}")
              .AppendLine()
              .AppendLine("| Platform | Scenario | Metric | Master (mean ± 95% CI) | Current (mean ± 95% CI) | Change | Overhead vs baseline (master → current) | Status |")
              .AppendLine("|----------|----------|--------|------------------------|-------------------------|--------|------------------------------------------|--------|")
              .Append(rows)
              .AppendLine();
        }

        return sb.ToString();
    }

    static string GetCompactTables(List<ScenarioComparison> comparisons)
    {
        var sb = new StringBuilder();
        foreach (var testGroup in comparisons.GroupBy(x => x.Test))
        {
            sb.AppendLine($"#### {testGroup.Key}")
              .AppendLine()
              .AppendLine($"| Platform | Scenario | {string.Join(" | ", Metrics.Select(x => x.DisplayName))} |")
              .AppendLine($"|----------|----------|{string.Join("|", Metrics.Select(_ => "---"))}|");

            foreach (var scenario in testGroup)
            {
                var cells = Metrics.Select(info =>
                {
                    var metric = scenario.Metrics.FirstOrDefault(x => x.Info == info);
                    return metric is null
                               ? "-"
                               : $"{info.FormatValue(metric.Current.Mean)} ({FormatPercent(metric.Change)}) {GetStatus(metric)}";
                });

                sb.AppendLine($"| {scenario.Platform} | {scenario.Scenario} | {string.Join(" | ", cells)} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    static string GetCharts(List<ScenarioComparison> comparisons)
    {
        const double msToNs = 1_000_000;
        const double zScore = 2.3263;
        const string offset = "    ";

        var sb = new StringBuilder();
        foreach (var group in comparisons.GroupBy(x => (x.Test, x.Platform)))
        {
            sb.AppendLine("```mermaid")
              .AppendLine("gantt")
              .AppendLine($"{offset}title Execution time (ms) {group.Key.Test} ({group.Key.Platform})")
              .AppendLine($"{offset}dateFormat  x")
              .AppendLine($"{offset}axisFormat %Q")
              .AppendLine($"{offset}todayMarker off");

            foreach (var scenario in group)
            {
                var isRegression = scenario.Metrics.Any(x => x.Info.Key == DurationKey && x.IsRegression);
                sb.AppendLine($"{offset}section {scenario.Scenario}");
                foreach (var result in new[] { scenario.CurrentResult, scenario.MasterResult }.Where(x => x is not null))
                {
                    var mean = result.Values[DurationKey].Mean;
                    var q05 = Math.Max(0, (mean - zScore * result.DurationStdev) / msToNs);
                    var q95 = (mean + zScore * result.DurationStdev) / msToNs;
                    var format = isRegression && result == scenario.CurrentResult ? "crit, " : string.Empty;
                    sb.AppendLine($"{offset}{result.Source.BranchName} - mean ({mean / msToNs:N0}ms)  : {format}{q05:F0}, {q95:F0}");
                }
            }

            sb.AppendLine("```").AppendLine();
        }

        return sb.ToString();
    }

    static string GetStatus(MetricComparison metric) => metric switch
    {
        { Master: null } => "🆕",
        { IsRegression: true } => "❌⬆️",
        { IsSlower: true } => "⚠️ noise",
        _ => "✅",
    };

    static string FormatWithCi(MetricInfo info, MetricValue value)
        => value is null
               ? "-"
               : $"{info.FormatValue(value.Mean)} ({info.FormatValue(value.Ci95Lower)} - {info.FormatValue(value.Ci95Upper)})";

    static string FormatPercent(double? value) => value switch
    {
        null => "-",
        >= 0 => $"+{value:F1}%",
        _ => $"{value:F1}%",
    };

    static string GetOsName(string os) => os switch
    {
        "windows" => "Windows",
        "linux" => "Linux",
        _ => os,
    };

    record MetricInfo(string Key, string DisplayName, double SignificanceScale, Func<double, string> FormatValue);

    record MetricValue(double Mean, double Ci95Lower, double Ci95Upper, double[] Data);

    record ProfilerResult(
        ExecutionTimeResultSource Source,
        string Test,
        string Platform,
        string Scenario,
        int ScenarioOrder,
        double DurationStdev,
        Dictionary<string, MetricValue> Values);

    record MetricComparison(MetricInfo Info, MetricValue Master, MetricValue Current, double? MasterOverhead, double? CurrentOverhead, bool IsSlower, bool IsRegression)
    {
        public double? Change => Master is null || Master.Mean == 0 ? null : (Current.Mean - Master.Mean) / Master.Mean * 100;
    }

    record ScenarioComparison(string Test, string Platform, string Scenario, ProfilerResult MasterResult, ProfilerResult CurrentResult, List<MetricComparison> Metrics);
}
