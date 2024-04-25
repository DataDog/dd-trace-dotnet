using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Nuke.Common;
using Nuke.Common.IO;
using Logger = Serilog.Log;

namespace ThroughputComparison;

public class CompareThroughput
{
    const decimal ThresholdForCritical = 0.95m;

    public static string GetMarkdown(List<CrankResultSource> sources)
    {
        Logger.Information("Reading crank results");
        var crankResults = sources.SelectMany(ReadJsonResults).ToList();

        // Group crankResults by test suite (result type), then 
        Logger.Information($"Found {crankResults.Count} results: building markdown");
        var charts = crankResults
                    .GroupBy(x => x.TestSuite)
                    .Select(group =>
                    {

                        var scenarios = group
                                       .Select(x => x)
                                       .OrderBy(x => x.Scenario)
                                       .GroupBy(x => x.Scenario)
                                       .Select(scenarioResults => GetMermaidSection(scenarioResults.Key, scenarioResults));
                        return $"""
                        ```mermaid
                        gantt
                            title Throughput {GetName(group.Key)} (Total requests) 
                            dateFormat  X
                            axisFormat %s
                        {string.Join(Environment.NewLine, scenarios)}
                        ```
                        """;
                    });

        return GetCommentMarkdown(sources, charts);
    }

    static string GetMermaidSection(CrankScenario scenario, IEnumerable<CrankResult> results)
    {
        const string offset = "    ";
        var sb = new StringBuilder();
        sb.Append(offset).Append("section ").AppendLine(GetName(scenario));
        var orderedResults = results.OrderBy(x => x.Source.SourceType).ToList();

        var threshold = orderedResults
                       .Where(x => x.Source.SourceType == CrankSourceType.Master)
                       .Select(x => (long?)(x.Requests * ThresholdForCritical))
                       .FirstOrDefault();

        foreach (var result in orderedResults)
        {
            var formattedRequests = (result.Requests / 1_000_000m).ToString("N3");
            var format = threshold.HasValue
                      && result.Source.SourceType == CrankSourceType.CurrentCommit
                      && scenario != CrankScenario.Baseline
                      && result.Requests <= threshold.Value
                             ? "crit ,"
                             : string.Empty;
            sb.Append(offset).Append(result.Source.BranchName)
              .Append(" (").Append(formattedRequests).Append("M)   : ")
              .Append(format).Append("0, ").Append(result.Requests)
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string GetCommentMarkdown(List<CrankResultSource> sources, IEnumerable<string> charts)
    {
        return $"""
            ## Throughput/Crank Report :zap:

            Throughput results for AspNetCoreSimpleController comparing the following branches/commits:
            {string.Join('\n', GetSourceMarkdown(sources))}

            Cases where throughput results for the PR are worse than latest master ({(int)((1 - ThresholdForCritical) * 100)}% drop or greater), results are shown in **red**.

            Note that these results are based on a _single_ point-in-time result for each branch. For full results, see [one](https://ddstaging.datadoghq.com/dashboard/fnz-afi-c2i/apm-net-crank) of the [many](https://ddstaging.datadoghq.com/dashboard/c5g-i7d-kcy/ciapp-apm-net-throughput-tests), [many](https://ddstaging.datadoghq.com/dashboard/uxh-m8j-qhi/ciapp-apm-net-throughput-tests-kevin) dashboards!

            {string.Join('\n', charts)}
            """;

        IEnumerable<string> GetSourceMarkdown(List<CrankResultSource> crankResultSources)
            => crankResultSources.Select(x => $"- {x.Markdown}");
    }

    private static string GetName(CrankTestSuite source) => source switch
    {
        CrankTestSuite.LinuxX64 => "Linux x64",
        CrankTestSuite.LinuxArm64 => "Linux arm64",
        CrankTestSuite.WindowsX64 => "Windows x64",
        CrankTestSuite.ASMLinuxX64 => "Linux x64 (ASM)",
        _ => throw new NotImplementedException(),
    };

    private static string GetName(CrankScenario source) => source switch
    {
        CrankScenario.Baseline => "Baseline",
        CrankScenario.AutomaticInstrumentation => "Automatic",
        CrankScenario.ManualInstrumentation => "Manual",
        CrankScenario.ManualAndAutomaticInstrumentation => "Manual + Automatic",
        CrankScenario.DdTraceEnabledFalse => "DD_TRACE_ENABLED=0",
        CrankScenario.TraceStats => "Trace stats",
        CrankScenario.NoAttack => "No attack",
        CrankScenario.AttackNoBlocking => "Attack",
        CrankScenario.AttackBlocking => "Blocking",
        CrankScenario.IastDefault => "IAST default",
        CrankScenario.IastFull => "IAST full",
        CrankScenario.IastVulnerabilityDisabled => "Base vuln",
        CrankScenario.IastVulnerabilityEnabled => "IAST vuln",
        _ => throw new NotImplementedException(),
    };

    static readonly (string Path, CrankTestSuite Type, (string Filename, CrankScenario Scenario)[] Scenarios)[] ExpectedScenarios =
    {
        ("crank_linux_x64_1", CrankTestSuite.LinuxX64, new[]
            {
                ("baseline_linux.json", CrankScenario.Baseline),
                ("calltarget_ngen_linux.json", CrankScenario.AutomaticInstrumentation),
                ("trace_stats_linux.json", CrankScenario.TraceStats),
                ("manual_only_linux.json", CrankScenario.ManualInstrumentation),
                ("manual_and_automatic_linux.json", CrankScenario.ManualAndAutomaticInstrumentation),
                ("ddtraceenabled_false_linux.json", CrankScenario.DdTraceEnabledFalse),
            }
        ),
        ("crank_linux_arm64_1", CrankTestSuite.LinuxArm64, new[]
            {
                ("baseline_linux_arm64.json", CrankScenario.Baseline),
                ("calltarget_ngen_linux_arm64.json", CrankScenario.AutomaticInstrumentation),
                ("trace_stats_linux_arm64.json", CrankScenario.TraceStats),
                ("manual_only_linux_arm64.json", CrankScenario.ManualInstrumentation),
                ("manual_and_automatic_linux_arm64.json", CrankScenario.ManualAndAutomaticInstrumentation),
                ("ddtraceenabled_false_linux_arm64.json", CrankScenario.DdTraceEnabledFalse),
            }
        ),
        ("crank_windows_x64_1", CrankTestSuite.WindowsX64, new[]
            {
                ("baseline_windows.json", CrankScenario.Baseline),
                ("calltarget_ngen_windows.json", CrankScenario.AutomaticInstrumentation),
                ("trace_stats_windows.json", CrankScenario.TraceStats),
                ("manual_only_windows.json", CrankScenario.ManualInstrumentation),
                ("manual_and_automatic_windows.json", CrankScenario.ManualAndAutomaticInstrumentation),
                ("ddtraceenabled_false_windows.json", CrankScenario.DdTraceEnabledFalse),
            }
        ),
        ("crank_linux_x64_asm_1", CrankTestSuite.ASMLinuxX64, new[]
            {
                ("appsec_baseline.json", CrankScenario.Baseline),
                ("appsec_noattack.json", CrankScenario.NoAttack),
                ("appsec_attack_noblocking.json", CrankScenario.AttackNoBlocking),
                ("appsec_attack_blocking.json", CrankScenario.AttackBlocking),
                ("appsec_iast_enabled_default.json", CrankScenario.IastDefault),
                ("appsec_iast_enabled_full.json", CrankScenario.IastFull),
                ("appsec_iast_disabled_vulnerability.json", CrankScenario.IastVulnerabilityDisabled),
                ("appsec_iast_enabled_vulnerability.json", CrankScenario.IastVulnerabilityEnabled),
            }
        ),
    };

    public static List<CrankResult> ReadJsonResults(CrankResultSource source)
    {
        var results = new List<CrankResult>();
        foreach (var (path, type, scenarios) in ExpectedScenarios)
            foreach (var (filename, scenario) in scenarios)
            {
                var fileName = source.Path / path / filename;
                try
                {
                    using var file = File.OpenRead(fileName);
                    var node = JsonNode.Parse(file)!;
                    var requests = (double)node["jobResults"]!["jobs"]!["load"]!["results"]!["bombardier/requests"];
                    results.Add(new(source, type, scenario, (long)requests));
                }
                catch (Exception ex)
                {
                    Logger.Information($"Error reading {fileName}: {ex.Message}. Skipping");
                }
            }

        return results;
    }

    public record CrankResult(CrankResultSource Source, CrankTestSuite TestSuite, CrankScenario Scenario, long Requests);

    public enum CrankTestSuite
    {
        LinuxX64,
        LinuxArm64,
        WindowsX64,
        ASMLinuxX64,
    }

    public enum CrankScenario
    {
        Baseline,
        AutomaticInstrumentation,
        TraceStats,
        ManualInstrumentation,
        ManualAndAutomaticInstrumentation,
        DdTraceEnabledFalse,
        NoAttack,
        AttackNoBlocking,
        AttackBlocking,
        IastDefault,
        IastFull,
        IastVulnerabilityDisabled,
        IastVulnerabilityEnabled,
    }
}

public record CrankResultSource(string BranchName, string CommitSha, CrankSourceType SourceType, AbsolutePath Path)
{
    public string Markdown => $"[{BranchName}](https://github.com/DataDog/dd-trace-dotnet/tree/{CommitSha})";
}

public enum CrankSourceType
{
    CurrentCommit,
    Master,
    LatestBenchmark,
    OldBenchmark,
}
