using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Nuke.Common.IO;

/// <summary>
/// Simple test to verify glob pattern matching behavior for pipeline excludes.
/// Reads the actual YAML file like the Nuke build does.
/// </summary>
public class TestGlobMatching
{
    public static void Test()
    {
        Console.WriteLine("Testing glob pattern matching for pipeline excludes\n");

        // Read exclude patterns from ultimate-pipeline.yml using PipelineParser
        var rootDirectory = (AbsolutePath)@"D:\source\datadog\dd-trace-dotnet";
        Console.WriteLine($"Root directory: {rootDirectory}");

        var tracerConfig = PipelineParser.GetPipelineDefinition(rootDirectory);
        var excludePatterns = tracerConfig.Pr?.Paths?.Exclude ?? Array.Empty<string>();

        Console.WriteLine($"Loaded {excludePatterns.Length} exclude patterns from YAML:\n");
        foreach (var pattern in excludePatterns)
        {
            Console.WriteLine($"  - {pattern}");
        }
        Console.WriteLine();

        var testCases = new[]
        {
            // Should be excluded
            new { Path = "docs/README.md", ShouldExclude = true },
            new { Path = "docs/development/Test.md", ShouldExclude = true },
            new { Path = ".github/workflows/test.yml", ShouldExclude = true },
            new { Path = "tracer/tools/Build-AzureFunctionsNuget.ps1", ShouldExclude = true },
            new { Path = "tracer/tools/Test.ps1", ShouldExclude = true },
            new { Path = "LICENSE", ShouldExclude = true },
            new { Path = ".azure-pipelines/noop-pipeline.yml", ShouldExclude = true },
            new { Path = "README.md", ShouldExclude = true },
            new { Path = "foo/bar/README.md", ShouldExclude = true },

            // Should NOT be excluded
            new { Path = "tracer/README.MD", ShouldExclude = false },
            new { Path = "tracer/src/Datadog.Trace/File.cs", ShouldExclude = false },
            new { Path = "tracer/build/script.ps1", ShouldExclude = false },
            new { Path = "tracer/tools/test.txt", ShouldExclude = false },
            new { Path = ".azure-pipelines/ultimate-pipeline.yml", ShouldExclude = false },
        };

        // Use the same matching logic as GetTracerStagesThatWillNotRun
        var matcher = new Matcher(StringComparison.Ordinal);
        foreach (var excludePath in excludePatterns)
        {
            // Normalize the pattern for the matcher
            // Azure DevOps treats trailing slashes as "match this directory and all descendants"
            var pattern = excludePath.EndsWith("/")
                ? excludePath + "**"  // Match directory and all descendants
                : excludePath;        // Match the exact file or use as-is for glob patterns

            matcher.AddInclude(pattern);
        }

        int passed = 0;
        int failed = 0;

        Console.WriteLine("Test Results:\n");
        foreach (var test in testCases)
        {
            var matchResult = matcher.Match(test.Path);
            var isExcluded = matchResult.HasMatches;
            var success = isExcluded == test.ShouldExclude;

            if (success)
            {
                Console.WriteLine($"✓ {test.Path} - correctly {(isExcluded ? "excluded" : "included")}");
                passed++;
            }
            else
            {
                Console.WriteLine($"✗ {test.Path} - expected {(test.ShouldExclude ? "excluded" : "included")}, got {(isExcluded ? "excluded" : "included")}");
                failed++;
            }
        }

        Console.WriteLine($"\n{passed} passed, {failed} failed");
        Environment.Exit(failed > 0 ? 1 : 0);
    }
}
