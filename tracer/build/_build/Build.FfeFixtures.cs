using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

partial class Build
{
    private static readonly HashSet<string> FfeFixtureCopyDisallowList = new(StringComparer.Ordinal)
    {
        ".git",
        ".github",
        ".gitignore",
        "ci",
        "CONTRIBUTING.md",
        "LICENSE",
        "LICENSE-3rdparty.csv",
        "NOTICE",
        "README.md",
        "SOURCE.md",
    };

    [Parameter("Branch, tag, or commit to copy from DataDog/ffe-system-test-data")]
    readonly string FfeFixtureRef = "main";

    Target UpdateFfeFixtures => _ => _
       .Description("Updates the checked-in FFE fixtures from DataDog/ffe-system-test-data")
       .Executes(() =>
       {
           ValidateFixtureRef(FfeFixtureRef);

           var destination = RootDirectory / "tracer" / "test" / "Datadog.Trace.Tests" / "FeatureFlags" / "ffe-system-test-data";
           var workingDirectory = TemporaryDirectory / $"ffe-system-test-data-{Guid.NewGuid():N}";
           var source = workingDirectory / "source";
           var snapshot = workingDirectory / "snapshot";
           var emptyGitConfig = workingDirectory / "empty-git-config";

           try
           {
               EnsureExistingDirectory(source);
               EnsureExistingDirectory(snapshot);
               File.WriteAllText(emptyGitConfig, string.Empty);

               var gitEnvironment = new Dictionary<string, string>
               {
                   ["GIT_CONFIG_NOSYSTEM"] = "1",
                   ["GIT_CONFIG_GLOBAL"] = emptyGitConfig,
               };

               RunGit(source, "init --quiet", gitEnvironment);
               RunGit(source, "remote add origin https://github.com/DataDog/ffe-system-test-data.git", gitEnvironment);
               RunGit(source, $"fetch --quiet --depth 1 origin {FfeFixtureRef}", gitEnvironment);
               RunGit(source, "checkout --quiet --detach FETCH_HEAD", gitEnvironment);
               var sourceCommit = RunGit(source, "rev-parse HEAD", gitEnvironment);

               CopyFixtureSnapshot(source, snapshot);
               var fixtureCount = ValidateFixtureSnapshot(snapshot);
               var changed = !HaveSameContents(snapshot, destination);

               if (changed)
               {
                   File.WriteAllText(
                       snapshot / "SOURCE.md",
                       $"""
# FFE Fixture Snapshot

These files are copied from the canonical FFE fixture repository.

Canonical source: https://github.com/DataDog/ffe-system-test-data
Source commit: {sourceCommit}

Do not edit these fixtures directly in dd-trace-dotnet. Add or update shared FFE behavior in ffe-system-test-data first, then refresh this snapshot.

The weekly update workflow runs `./tracer/build.sh UpdateFfeFixtures` and opens a draft test PR only when the allowed fixture contents change.
""");

                   EnsureCleanDirectory(destination);
                   CopyDirectory(snapshot, destination);
               }

               Logger.Information("Checked FFE fixtures from DataDog/ffe-system-test-data@{SourceCommit}", sourceCommit);
               Logger.Information("Loaded {FixtureCount} JSON fixture cases", fixtureCount);
               Logger.Information("Fixture snapshot changed: {Changed}", changed);

               var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
               if (!string.IsNullOrWhiteSpace(githubOutput))
               {
                   File.AppendAllLines(
                       githubOutput,
                       new[]
                       {
                           $"source_commit={sourceCommit}",
                           $"fixture_count={fixtureCount}",
                           $"changed={changed.ToString().ToLowerInvariant()}",
                       });
               }
           }
           finally
           {
               DeleteDirectory(workingDirectory);
           }
       });

    private static void ValidateFixtureRef(string fixtureRef)
    {
        if (string.IsNullOrWhiteSpace(fixtureRef)
         || fixtureRef.StartsWith("-", StringComparison.Ordinal)
         || fixtureRef.Contains("..", StringComparison.Ordinal)
         || fixtureRef.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '/' or '-')))
        {
            throw new ArgumentException($"Invalid FFE fixture ref: {fixtureRef}", nameof(fixtureRef));
        }
    }

    private static string RunGit(AbsolutePath workingDirectory, string arguments, IReadOnlyDictionary<string, string> environment)
    {
        var process = ProcessTasks.StartProcess(
            "git",
            arguments,
            workingDirectory,
            environmentVariables: environment,
            logOutput: false);
        process.AssertZeroExitCode();
        return string.Join(Environment.NewLine, process.Output.Select(line => line.Text)).Trim();
    }

    private static void CopyFixtureSnapshot(AbsolutePath source, AbsolutePath snapshot)
    {
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            if (FfeFixtureCopyDisallowList.Contains(entry.Name))
            {
                continue;
            }

            CopyEntry(entry, Path.Combine(snapshot, entry.Name));
        }
    }

    private static void CopyEntry(FileSystemInfo source, string destination)
    {
        if ((source.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Refusing to copy symbolic link from FFE fixture repository: {source.FullName}");
        }

        if (source is FileInfo file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            file.CopyTo(destination, overwrite: true);
            return;
        }

        Directory.CreateDirectory(destination);
        foreach (var child in ((DirectoryInfo)source).EnumerateFileSystemInfos())
        {
            CopyEntry(child, Path.Combine(destination, child.Name));
        }
    }

    private static int ValidateFixtureSnapshot(AbsolutePath snapshot)
    {
        var configPath = snapshot / "ufc-config.json";
        var casesDirectory = snapshot / "evaluation-cases";
        if (!File.Exists(configPath) || !Directory.Exists(casesDirectory))
        {
            throw new InvalidOperationException("FFE fixture repository does not contain the expected fixture layout");
        }

        using (var config = JsonDocument.Parse(File.ReadAllText(configPath)))
        {
        }

        var caseFiles = Directory.GetFiles(casesDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (caseFiles.Length == 0)
        {
            throw new InvalidOperationException("No FFE JSON fixture files found");
        }

        var fixtureCount = 0;
        foreach (var caseFile in caseFiles)
        {
            using var cases = JsonDocument.Parse(File.ReadAllText(caseFile));
            if (cases.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"{caseFile} must contain a JSON array of test cases");
            }

            fixtureCount += cases.RootElement.GetArrayLength();
        }

        if (fixtureCount == 0)
        {
            throw new InvalidOperationException("No FFE fixture test cases found");
        }

        return fixtureCount;
    }

    private static bool HaveSameContents(AbsolutePath snapshot, AbsolutePath destination)
    {
        if (!Directory.Exists(destination))
        {
            return false;
        }

        var snapshotFiles = GetRelativeFiles(snapshot);
        var destinationFiles = GetRelativeFiles(destination, "SOURCE.md");
        if (!snapshotFiles.SequenceEqual(destinationFiles, StringComparer.Ordinal))
        {
            return false;
        }

        return snapshotFiles.All(relativePath =>
            File.ReadAllBytes(snapshot / relativePath).AsSpan().SequenceEqual(File.ReadAllBytes(destination / relativePath)));
    }

    private static string[] GetRelativeFiles(AbsolutePath directory, params string[] excludedFiles)
    {
        return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(directory, path))
                        .Where(path => !excludedFiles.Contains(path, StringComparer.Ordinal))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();
    }

    private static void CopyDirectory(AbsolutePath source, AbsolutePath destination)
    {
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            CopyEntry(entry, Path.Combine(destination, entry.Name));
        }
    }
}
