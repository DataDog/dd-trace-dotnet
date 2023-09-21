using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nietras.SeparatedValues;
using Nuke.Common.IO;

#nullable enable

namespace CriticalPathAnalysis;

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

    public static Task AnalyzeCriticalPath(AbsolutePath rootDirectory)
    {
        var pipeline = PipelineParser.GetPipelineDefinition(rootDirectory);
        var stages = GetStages(rootDirectory, pipeline);
        stages = OrderByDependencies(stages);
        UpdateEarliestValues(stages);
        UpdateLatestValues(stages);
        // displaying in original (pipeline) order
        var stageOrder = pipeline.Stages.Select((x, i) => (x.Stage, Index: i)).ToDictionary(x => x.Stage, x => x.Index);
        var ordered = stages.OrderBy(x => stageOrder[x.Id]);
        var mermaidDiagram = GenerateMermaidDiagram(ordered);
        var outputPath = rootDirectory / "tracer" / "build_data" / "pipeline_critical_path.md";
        File.WriteAllText(outputPath, $"""
                                       ```mermaid
                                       {mermaidDiagram}
                                       ```
                                       """);
        Serilog.Log.Information("Created mermaid diagram at {OutputPath}", outputPath);
        return Task.CompletedTask;
    }

    static List<PipelineStage> GetStages(AbsolutePath rootDirectory, PipelineDefinition pipeline)
    {
        var stages = new List<PipelineStage>(pipeline.Stages.Length);

        // Export the data from the widget here by clicking "Download as csv" and moving to tracer/build_data/stages.csv
        // https://ddstaging.datadoghq.com/dashboard/74k-me3-y2z?from_ts=1689611447016&to_ts=1692289847016&live=true
        var stagesCsv = rootDirectory / "tracer" / "build_data" / "stages.csv";
        using var reader = Sep.New(',').Reader().FromFile(stagesCsv);
        foreach (var row in reader)
        {
            var stageName = row[0].ToString();
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
            var stageDefinition = pipeline.Stages.First(x => x.Stage == currentStage.Id);

            foreach (var dependency in stageDefinition.DependsOn)
            {
                var predecessor = stages.First(x => x.Id == dependency);
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

    static string GenerateMermaidDiagram(IEnumerable<PipelineStage> stages)
    {
        // can't easily show flex in the diagram, so display using earliest start/end
        // and mark critical tasks
        var sb = new StringBuilder();
        sb.AppendLine("""
             gantt
                 title Consolidated pipeline critical path analysis
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
