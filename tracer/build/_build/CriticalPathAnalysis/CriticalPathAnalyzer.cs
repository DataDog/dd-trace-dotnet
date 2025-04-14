using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nietras.SeparatedValues;
using Nuke.Common.IO;
using Logger = Serilog.Log;

#nullable enable

namespace CriticalPathAnalysis;

/// <summary>
/// Implements the Critical Path Method
/// </summary>
public static class CriticalPathAnalyzer
{
    // These checks are required for merging
    // Copied from https://github.com/DataDog/dd-trace-dotnet/settings/branch_protection_rules
    private static readonly string[] RequiredStagesForMerging =
    {
        "unit_tests_arm64",
        "unit_tests_linux",
        "unit_tests_windows",
        "integration_tests_arm64",
        "integration_tests_linux",
        "integration_tests_windows",
        "integration_tests_windows_iis",
        "dotnet_tool",
        "tool_artifacts_tests_linux",
        "tool_artifacts_tests_windows",
        "msi_integration_tests_windows",
        "installer_smoke_tests",
        "installer_smoke_tests_arm64",
        "code_freeze",
        "profiler_integration_tests_linux",
        "tracer_home_smoke_tests",
        "verify_source_generators",
        "verify_app_trimming_descriptor_generator",
    };

    // These stages are ignored from the analysis, primarily because they are not going to be around for much longer
    static readonly string[] AlwaysIgnoreStages =
    {
        "throughput",
        "benchmarks",
        "coverage", // optional, basically never run
        "integration_tests_windows_iis_security", // removed recently
    };

    // These stages don't run on PRs, but do run on master, so exclude from the analysis for PRs only
    static readonly string[] MasterOnlyStages =
    {
        "upload_container_images",
        "nuget_installer_smoke_tests",
        "nuget_installer_smoke_tests_arm64",
        "nuget_installer_smoke_tests_windows",
        "dotnet_tool_nuget_smoke_tests_linux",
        "dotnet_tool_nuget_smoke_tests_macos",
        "dotnet_tool_smoke_tests_linux",
        "dotnet_tool_self_instrument_smoke_tests_linux",
        "dotnet_tool_smoke_tests_arm64",
        "dotnet_tool_smoke_tests_windows",
        "dd_dotnet_msi_installer_smoke_tests",
        "dd_dotnet_installer_failure_tests_linux_arm64",
        "msi_installer_smoke_tests",
    };

    public static Task AnalyzeCriticalPath(AbsolutePath rootDirectory, bool isMasterRun)
    {
        var ignoreStages = isMasterRun ? AlwaysIgnoreStages : MasterOnlyStages.Concat(AlwaysIgnoreStages).ToArray();
        var pipeline = PipelineParser.GetPipelineDefinition(rootDirectory);
        var stages = GetStages(rootDirectory, pipeline, ignoreStages);
        stages = OrderByDependencies(stages);
        UpdateEarliestValues(stages);
        UpdateLatestValues(stages);
        // displaying in original (pipeline) order
        var stageOrder = pipeline.Stages.Select((x, i) => (x.Stage, Index: i)).ToDictionary(x => x.Stage, x => x.Index);
        var ordered = stages.OrderBy(x => stageOrder[x.Id]);
        var mermaidDiagram = GenerateMermaidDiagram(ordered, isMasterRun);
        var outputPath = rootDirectory / "artifacts" / "build_data" / "pipeline_critical_path.md";
        File.WriteAllText(outputPath, $"""
                                       ```mermaid
                                       {mermaidDiagram}
                                       ```
                                       """);
        Logger.Information("Created mermaid diagram at {OutputPath}", outputPath);
        return Task.CompletedTask;
    }

    static List<PipelineStage> GetStages(AbsolutePath rootDirectory, PipelineDefinition pipeline, string[] IgnoreStages)
    {
        var stages = new List<PipelineStage>(pipeline.Stages.Length);

        // Export the data from the widget here by clicking "Download as csv" and moving to artifacts/build_data/stages.csv
        // https://app.datadoghq.com/dashboard/49i-n6n-9jq
        var stagesCsv = rootDirectory / "artifacts" / "build_data" / "stages.csv";
        using var reader = Sep.New(',').Reader().FromFile(stagesCsv);
        foreach (var row in reader)
        {
            var stageName = row[0].ToString();
            if (IgnoreStages.Contains(stageName))
            {
                continue;
            }

            var durationInNanosecond = row[1].Parse<decimal>();
            var durationInMilliSeconds = (long)(durationInNanosecond / 1_000_000m);
            var requiredForMerging = RequiredStagesForMerging.Contains(stageName);

            var stage = new PipelineStage(new(stageName), durationInMilliSeconds, requiredForMerging);
            stages.Add(stage);
        }

        // Fix the predecessors (depends_on) and 
        // the successors (stages that depend on us)
        foreach (var currentStage in stages)
        {
            var stageDefinition = pipeline.Stages.FirstOrDefault(x => x.Stage == currentStage.Id);
            if (stageDefinition is null)
            {
                throw new Exception($"Could not find stage called {currentStage.Id}. " );
            }

            foreach (var dependency in stageDefinition.DependsOn)
            {
                if (IgnoreStages.Contains(dependency))
                {
                    continue;
                }

                var predecessor = stages.FirstOrDefault(x => x.Id == dependency);
                if (predecessor is null)
                {
                    throw new Exception($"{stageDefinition.Stage} depends on stage '{dependency}' that could not be found in output results");
                }
                currentStage.Predecessors.Add(predecessor);
                predecessor.Successors.Add(currentStage);
            }
        }

        return stages;
    }

    static void UpdateEarliestValues(List<PipelineStage> stages)
    {
        foreach (var stage in stages)
        {
            long earliestStart = 0;
            foreach (var predecessor in stage.Predecessors)
            {
                var predecessorRange = predecessor.Earliest;

                if (earliestStart < predecessorRange.End)
                {
                    earliestStart = predecessorRange.End;
                }
            }

            var earliestEnd = earliestStart + stage.Duration;
            stage.Earliest = new(earliestStart, earliestEnd);
        }
    }

    static void UpdateLatestValues(List<PipelineStage> stages)
    {
        // we bootstrap the last stage by setting LatestEnd = EarliestEnd and calculating the corresponding start
        var pipelineEnds = stages.Select(x => x.Earliest.End).Max();

        // Now walk backwards 
        foreach (var stage in stages.AsEnumerable().Reverse())
        {
            var latestEnd = pipelineEnds;
            foreach (var successor in stage.Successors)
            {
                if (latestEnd > successor.Latest.Start)
                {
                    latestEnd = successor.Latest.Start;
                }
            }

            var latestStart = latestEnd - stage.Duration;
            stage.Latest = new(latestStart, latestEnd);
        }
    }

    static List<PipelineStage> OrderByDependencies(List<PipelineStage> stages)
    {
        var processedPairs = new HashSet<string>();
        var totalCount = stages.Count;
        var ordered = new List<PipelineStage>(totalCount);
        while (ordered.Count < totalCount) {
            var foundSomethingToProcess = false;
            foreach (var kvp in stages)
            {
                if (!processedPairs.Contains(kvp.Id)
                 && kvp.Predecessors.All(x => processedPairs.Contains(x.Id)))
                {
                    ordered.Add(kvp);
                    processedPairs.Add(kvp.Id);
                    foundSomethingToProcess = true;
                }
            }

            if (!foundSomethingToProcess)
            {
                throw new InvalidOperationException("Loop detected inside stage dependencies");
            }
        }

        return ordered;
    }

    static string GenerateMermaidDiagram(IEnumerable<PipelineStage> stages, bool isMaster)
    {
        // can't easily show flex in the diagram, so display using earliest start/end
        // and mark critical tasks
        var sb = new StringBuilder();
        sb.Append("""
             gantt
                 title Consolidated pipeline critical path analysis
             """);
        sb.AppendLine(isMaster ? " for master" : " for branches");
        sb.AppendLine("""
                 dateFormat s
                 axisFormat %H:%M
                 todayMarker off
             """);

        foreach (var stage in stages)
        {
            var duration = TimeSpan.FromMilliseconds(stage.Duration) switch
            {
                var x when x.TotalMinutes > 60 => $"{x.Hours}h {x.Minutes}mins",
                var x when x.TotalMinutes > 1 => $"{x.Minutes}mins",
                var x when x.TotalSeconds > 1 => $"{x.Seconds}s",
                _ => "<1s",
            };
            
            sb
               .Append("    ")
               .Append(stage.Id)
               .Append(" (")
               .Append(duration)
               .Append(")  : ");

            if (!stage.RequiredForMerging)
            {
                sb.Append("done, ");
            }

            if (stage.IsOnCriticalPath)
            {
                sb.Append("crit, ");
            }

            // specify alias
            sb.Append(stage.Id).Append(", ");

            if (stage.Predecessors.Count > 0)
            {
                sb.Append("after ");
                foreach (var predecessor in stage.Predecessors)
                {
                    sb.Append(predecessor.Id).Append(' ');
                }

                sb.Append(", ");
            }
            else
            {
                // if we don't have an after, we have to specify the start time (assume on app start)
                sb.Append("0, ");
            }

            // ms doesnt seem to work, so use seconds
            sb.Append(stage.Duration / 1_000).Append("s").AppendLine();
        }

        return sb.ToString();
    }

    record PipelineStage(string Id, long Duration, bool RequiredForMerging)
    {
        public List<PipelineStage> Predecessors { get; } = new();
        public List<PipelineStage> Successors { get; } = new();

        public Range Earliest { get; set; } = Range.Zero;
        public Range Latest { get; set; } = Range.Zero;

        public bool IsOnCriticalPath => Earliest == Latest;

        public override string ToString() => Id;
    }

    record Range(long Start, long End)
    {
        public static readonly Range Zero = new(0, 0);
    }
}
