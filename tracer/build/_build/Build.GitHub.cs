using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BenchmarkComparison;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.Git;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using ThroughputComparison;
using YamlDotNet.Serialization.NamingConventions;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using Issue = Octokit.Issue;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Target = Nuke.Common.Target;
using static Octokit.GraphQL.Variable;
using Environment = System.Environment;
using Milestone = Octokit.Milestone;
using Release = Octokit.Release;
using Logger = Serilog.Log;

partial class Build
{
    [Parameter("A GitHub token (for use in GitHub Actions)", Name = "GITHUB_TOKEN")]
    readonly string GitHubToken;

    [Parameter("Git repository name", Name = "GITHUB_REPOSITORY_NAME", List = false)]
    readonly string GitHubRepositoryName = "dd-trace-dotnet";

    [Parameter("An Azure Devops PAT (for use in GitHub Actions)", Name = "AZURE_DEVOPS_TOKEN")]
    readonly string AzureDevopsToken;

    [Parameter("Azure Devops pipeline id", Name = "AZURE_DEVOPS_PIPELINE_ID", List = false)]
    readonly int AzureDevopsConsolidatePipelineId = 54;

    [Parameter("Azure Devops project id", Name = "AZURE_DEVOPS_PROJECT_ID", List = false)]
    readonly Guid AzureDevopsProjectId = Guid.Parse("a51c4863-3eb4-4c5d-878a-58b41a049e4e");

    [Parameter("The Pull Request number for GitHub Actions")]
    readonly int? PullRequestNumber;

    [Parameter("The specific commit sha to use", List = false)]
    readonly string CommitSha;

    [Parameter("The specific Azure DevOps Build ID to use", List = false)]
    readonly int? AzureDevopsBuildId;

    [Parameter("The git branch to use", List = false)]
    readonly string TargetBranch;

    [Parameter("Is the ChangeLog expected to change?", List = false)]
    readonly bool ExpectChangelogUpdate = true;

    const string GitHubRepositoryOwner = "DataDog";
    const string AzureDevopsOrganisation = "https://dev.azure.com/datadoghq";

    string FullVersion => IsPrerelease ? $"{Version}-prerelease" : Version;

    Target AssignPullRequestToMilestone => _ => _
       .Unlisted()
       .Requires(() => GitHubRepositoryName)
       .Requires(() => GitHubToken)
       .Requires(() => PullRequestNumber)
       .Executes(async() =>
        {
            var client = GetGitHubClient();

            var milestone = await GetOrCreateVNextMilestone(client);

            Console.WriteLine($"Assigning PR {PullRequestNumber} to {milestone.Title} ({milestone.Number})");

            await client.Issue.Update(
                owner: GitHubRepositoryOwner,
                name: GitHubRepositoryName,
                number: PullRequestNumber.Value,
                new IssueUpdate { Milestone = milestone.Number });

            Console.WriteLine($"PR assigned");
        });

    Target SummaryOfSnapshotChanges => _ => _
           .Unlisted()
           .Requires(() => GitHubRepositoryName)
           .Requires(() => GitHubToken)
           .Requires(() => PullRequestNumber)
           .Requires(() => TargetBranch)
           .Executes(async() =>
            {
                // This assumes that we're running in a pull request, so we compare against the target branch
                var baseCommit = GitTasks.Git($"merge-base origin/{TargetBranch} HEAD").First().Text;

                // This is a dumb implementation that just show the diff
                // We could imagine getting the whole context with -U1000 and show the differences including parent name
                // eg now we show -oldAttribute: oldValue, but we could show -tag.oldattribute: oldvalue
                var changes = GitTasks.Git($"diff --diff-filter=M \"{baseCommit}\" -- */*snapshots*/*.*")
                                   .Select(f => f.Text);

                if (!changes.Any())
                {
                    Console.WriteLine($"No snapshots modified (some may have been added/deleted). Not doing snapshots diff.");
                    return;
                }

                const string unlinkedLinesExplicitor = "[...]";
                var crossVersionTestsNamePattern = new [] {"VersionMismatchNewerNugetTests"};
                var diffCounts = new Dictionary<string, int>();
                StringBuilder diffsInFile = new();
                var considerUpdatingPublicFeed = false;
                var lastLine = string.Empty;
                foreach (var line in changes)
                {
                    if (line.StartsWith("@@ ")) // new change, not looking at files cause the changes would be too different
                    {
                        RecordChange(diffsInFile, diffCounts);
                        lastLine = String.Empty;
                        continue;
                    }

                    if (line.StartsWith("- ") || line.StartsWith("+ "))
                    {
                        if (!string.IsNullOrEmpty(lastLine) &&
                            lastLine[0] != line[0] &&
                            lastLine.Trim(',').Substring(1) == line.Trim(',').Substring(1))
                        {
                            // The two lines are actually the same, just an additional comma on previous line
                            // So we can remove it from the diff for better understanding?
                            diffsInFile.Remove(diffsInFile.Length - lastLine.Length - Environment.NewLine.Length, lastLine.Length + Environment.NewLine.Length);
                            lastLine = string.Empty;
                            continue;
                        }

                        diffsInFile.AppendLine(line);
                        lastLine = line;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(lastLine))
                    {
                        diffsInFile.AppendLine(unlinkedLinesExplicitor);
                        lastLine = string.Empty;
                    }

                    if (!considerUpdatingPublicFeed && crossVersionTestsNamePattern.Any(p => line.Contains(p)))
                    {
                        considerUpdatingPublicFeed = true;
                    }
                }

                RecordChange(diffsInFile, diffCounts);

                var markdown = new StringBuilder();
                markdown.AppendLine("## Snapshots difference summary").AppendLine();
                markdown.AppendLine("The following differences have been observed in committed snapshots. It is meant to help the reviewer.");
                markdown.AppendLine("The diff is simplistic, so please check some files anyway while we improve it.").AppendLine();

                if (considerUpdatingPublicFeed)
                {
                    markdown.AppendLine("**Note** that this PR updates a version mismatch test. You may need to upgrade your code in the Azure public feed");
                }

                foreach (var diff in diffCounts)
                {
                    markdown.AppendLine($"{diff.Value} occurrences of : ");
                    markdown.AppendLine("```diff");
                    markdown.AppendLine(diff.Key);
                    markdown.Append("```").AppendLine();
                }

                // Console.WriteLine(markdown.ToString());
                await ReplaceCommentInPullRequest(PullRequestNumber.Value, "## Snapshots difference", markdown.ToString());

                void RecordChange(StringBuilder diffsInFile, Dictionary<string, int> diffCounts)
                {
                    var unlinkedLinesExplicitorWithNewLine = unlinkedLinesExplicitor + Environment.NewLine;

                    if (diffsInFile.Length > 0)
                    {
                        var change = diffsInFile.ToString();
                        if (change.EndsWith(unlinkedLinesExplicitorWithNewLine))
                        {
                            change = change.Substring(0, change.Length - unlinkedLinesExplicitorWithNewLine.Length);
                        }
                        diffCounts.TryAdd(change, 0);
                        diffCounts[change]++;
                        diffsInFile.Clear();
                    }
                }
            });

    Target AssignLabelsToPullRequest => _ => _
       .Unlisted()
       .Requires(() => GitHubRepositoryName)
       .Requires(() => GitHubToken)
       .Requires(() => PullRequestNumber)
       .Executes(async() =>
        {
            var client = GetGitHubClient();

            var pr = await client.PullRequest.Get(
                owner: GitHubRepositoryOwner,
                name: GitHubRepositoryName,
                number: PullRequestNumber.Value);

            // Fixes an issue (ambiguous argument) when we do git diff in the Action.
            GitTasks.Git("fetch origin master:master", logOutput: false);
            var changedFiles = GitTasks.Git("diff --name-only master").Select(f => f.Text);
            var config = GetLabellerConfiguration();
            Console.WriteLine($"Checking labels for PR {PullRequestNumber}");

            var updatedLabels = ComputeLabels(config, pr.Title, pr.Labels.Select(l => l.Name), changedFiles);
            var issueUpdate = new IssueUpdate();
            updatedLabels.ForEach(l => issueUpdate.AddLabel(l));

            try
            {
                await client.Issue.Update(
                    owner: GitHubRepositoryOwner,
                    name: GitHubRepositoryName,
                    number: PullRequestNumber.Value,
                    issueUpdate);
            }
            catch(Exception ex)
            {
                Logger.Warning($"An error happened while updating the labels on the PR: {ex}");
            }

            Console.WriteLine($"PR labels updated");

            HashSet<String> ComputeLabels(LabbelerConfiguration config, string prTitle, IEnumerable<string> labels, IEnumerable<string> changedFiles)
            {
                var updatedLabels = new HashSet<string>(labels);

                foreach(var label in config.Labels)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(label.Title))
                        {
                            Console.WriteLine("Checking if pr title matches: " + label.Title);
                            var regex = new Regex(label.Title, RegexOptions.Compiled);
                            if (regex.IsMatch(prTitle))
                            {
                                Console.WriteLine("Yes it does. Adding label " + label.Name);
                                updatedLabels.Add(label.Name);
                            }
                        }
                        else if (!string.IsNullOrEmpty(label.AllFilesIn))
                        {
                            Console.WriteLine("Checking if changed files are all located in:" + label.AllFilesIn);
                            var regex = new Regex(label.AllFilesIn, RegexOptions.Compiled);
                            if(!changedFiles.Any(x => !regex.IsMatch(x)))
                            {
                                Console.WriteLine("Yes they do. Adding label " + label.Name);
                                updatedLabels.Add(label.Name);
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Logger.Warning($"There was an error trying to check labels: {ex}");
                    }
                }
                return updatedLabels;
            }

           LabbelerConfiguration GetLabellerConfiguration()
           {
               var labellerConfigYaml = RootDirectory / ".github" / "labeller.yml";
               Logger.Information($"Reading {labellerConfigYaml} YAML file");
               var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                 .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                 .IgnoreUnmatchedProperties()
                                 .Build();

               using var sr = new StreamReader(labellerConfigYaml);
               return deserializer.Deserialize<LabbelerConfiguration>(sr);
           }
        });

    Target CloseMilestone => _ => _
       .Unlisted()
       .Requires(() => GitHubToken)
       .Requires(() => Version)
       .Executes(async() =>
       {
            var client = GetGitHubClient();

            var milestone = await GetMilestone(client, Version);
            if (milestone is null)
            {
                Console.WriteLine($"Milestone {Version} not found. Doing nothing");
                return;
            }

            Console.WriteLine($"Closing {milestone.Title}");

            try
            {
                await client.Issue.Milestone.Update(
                    owner: GitHubRepositoryOwner,
                    name: GitHubRepositoryName,
                    number: milestone.Number,
                    new MilestoneUpdate { State = ItemState.Closed });
            }
            catch (ApiValidationException ex)
            {
                Console.WriteLine($"Unable to close {milestone.Title}. Exception: {ex}");
                return; // shouldn't be blocking
            }

            Console.WriteLine($"Milestone closed");
        });

    Target RenameVNextMilestone => _ => _
       .Unlisted()
       .Requires(() => GitHubRepositoryName)
       .Requires(() => GitHubToken)
       .Requires(() => Version)
       .Executes(async() =>
        {
            var client = GetGitHubClient();

            var milestone = await GetOrCreateVNextMilestone(client);

            Console.WriteLine($"Updating {milestone.Title} to {FullVersion}");

            try
            {
                await client.Issue.Milestone.Update(
                    owner: GitHubRepositoryOwner,
                    name: GitHubRepositoryName,
                    number: milestone.Number,
                    new MilestoneUpdate { Title = FullVersion });
            }
            catch (ApiValidationException)
            {
                Console.WriteLine($"Unable to rename {milestone.Title} milestone to {FullVersion}: does this milestone already exist?");
                throw;
            }

            Console.WriteLine($"Milestone renamed");
            // set the output variable
            Console.WriteLine("::set-output name=milestone::" + milestone.Number);
        });

    Target OutputCurrentVersionToGitHub => _ => _
       .Unlisted()
       .After(UpdateVersion)
       .Requires(() => Version)
       .Executes(async () =>
        {
            Console.WriteLine("Using version to " + FullVersion);
            Console.WriteLine("::set-output name=version::" + Version);
            Console.WriteLine("::set-output name=full_version::" + FullVersion);
            Console.WriteLine("::set-output name=isprerelease::" + (IsPrerelease ? "true" : "false"));
            Console.WriteLine("::set-output name=lib_waf_version::" + LibDdwafVersion);

            var rulesPath = Solution.GetProject(Projects.DatadogTrace)!.Directory / "AppSec" / "Waf" / "ConfigFiles" / "rule-set.json";
            await using var rules = File.OpenRead(rulesPath);
            using var doc = await JsonDocument.ParseAsync(rules, new JsonDocumentOptions { AllowTrailingCommas = true });
            var rulesVersion = doc.RootElement.GetProperty("metadata").GetProperty("rules_version").GetString();
            if (string.IsNullOrEmpty(rulesVersion))
            {
                throw new Exception($"There was an error reading the metadata.rules_version element from {rulesPath}");
            }

            Console.WriteLine("::set-output name=waf_rules_version::" + rulesVersion);
        });

    Target CalculateNextVersion => _ => _
       .Unlisted()
       .Requires(() => Version)
       .Executes(() =>
        {
            Console.WriteLine("Current version is " + Version);
            var parsedVersion = new Version(Version);
            var major = parsedVersion.Major;
            int minor;
            int patch;

            if (major == 1)
            {
                // always do patch version bump on 1.x branch
                minor = parsedVersion.Minor;
                patch = parsedVersion.Build + 1;
            }
            else
            {
                // always do minor version bump on 2.x branch
                minor = parsedVersion.Minor + 1;
                patch = 0;
            }

            var nextVersion = $"{major}.{minor}.{patch}";

            Console.WriteLine("Next version calculated as " + FullVersion);
            Console.WriteLine("::set-output name=version::" + nextVersion);
            Console.WriteLine("::set-output name=full_version::" + nextVersion);
            Console.WriteLine("::set-output name=previous_version::" + Version);
            Console.WriteLine("::set-output name=isprerelease::false");
        });

    Target VerifyChangedFilesFromVersionBump => _ => _
       .Unlisted()
       .Description("Verifies that the expected files were changed")
       .After(UpdateVersion, UpdateChangeLog)
       .Executes(() =>
        {
            var expectedFileChanges = new List<string>
            {
                ".azure-pipelines/ultimate-pipeline.yml",
                "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/CMakeLists.txt",
                "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Windows/Resource.rc",
                "profiler/src/ProfilerEngine/Datadog.Profiler.Native/dd_profiler_version.h",
                "profiler/src/ProfilerEngine/Datadog.Linux.ApiWrapper/CMakeLists.txt",
                "profiler/src/ProfilerEngine/ProductVersion.props",
                "shared/src/Datadog.Trace.ClrProfiler.Native/CMakeLists.txt",
                "shared/src/Datadog.Trace.ClrProfiler.Native/Resource.rc",
                "shared/src/msi-installer/WindowsInstaller.wixproj",
                "shared/src/native-src/version.h",
                "tracer/build/artifacts/dd-dotnet.sh",
                "tracer/build/_build/Build.cs",
                "tracer/samples/AutomaticTraceIdInjection/MicrosoftExtensionsExample/MicrosoftExtensionsExample.csproj",
                "tracer/samples/AutomaticTraceIdInjection/Log4NetExample/Log4NetExample.csproj",
                "tracer/samples/AutomaticTraceIdInjection/NLog40Example/NLog40Example.csproj",
                "tracer/samples/AutomaticTraceIdInjection/NLog45Example/NLog45Example.csproj",
                "tracer/samples/AutomaticTraceIdInjection/NLog46Example/NLog46Example.csproj",
                "tracer/samples/AutomaticTraceIdInjection/SerilogExample/SerilogExample.csproj",
                "tracer/samples/ConsoleApp/Alpine3.10.dockerfile",
                "tracer/samples/ConsoleApp/Alpine3.9.dockerfile",
                "tracer/samples/ConsoleApp/Debian.dockerfile",
                "tracer/samples/OpenTelemetry/Debian.dockerfile",
                "tracer/samples/WindowsContainer/Dockerfile",
                "tracer/src/Datadog.Trace.Bundle/Datadog.Trace.Bundle.csproj",
                "tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj",
                "tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                "tracer/src/Datadog.Trace.Manual/Datadog.Trace.Manual.csproj",
                "tracer/src/Datadog.Tracer.Native/CMakeLists.txt",
                "tracer/src/Datadog.Tracer.Native/dd_profiler_constants.h",
                "tracer/src/Datadog.Tracer.Native/Resource.rc",
                "tracer/src/Datadog.Trace.MSBuild/Datadog.Trace.MSBuild.csproj",
                "tracer/src/Datadog.Trace.BenchmarkDotNet/Datadog.Trace.BenchmarkDotNet.csproj",
                "tracer/src/Datadog.Trace.OpenTracing/Datadog.Trace.OpenTracing.csproj",
                "tracer/src/Datadog.Trace.Tools.dd_dotnet/Datadog.Trace.Tools.dd_dotnet.csproj",
                "tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj",
                "tracer/src/Datadog.Trace/Datadog.Trace.csproj",
                "tracer/src/Datadog.Trace/TracerConstants.cs",
                "tracer/src/Datadog.Trace.Trimming/Datadog.Trace.Trimming.csproj",
                "tracer/tools/PipelineMonitor/PipelineMonitor.csproj",
            };

            if (ExpectChangelogUpdate)
            {
                expectedFileChanges.Insert(0, "docs/CHANGELOG.md");
            }

            Logger.Information("Verifying that all expected files changed...");
            var changes = GitTasks.Git("diff --name-only");
            var stagedChanges = GitTasks.Git("diff --name-only --staged");

            var allChanges = changes
                            .Concat(stagedChanges)
                            .Where(x => x.Type == OutputType.Std)
                            .Select(x => x.Text)
                            .ToHashSet();

            var missingChanges = expectedFileChanges
                                .Where(x => !allChanges.Contains(x))
                                .ToList();

            if (missingChanges.Any())
            {
                foreach (var missingChange in missingChanges)
                {
                    Logger.Error($"::error::Expected change not found in file '{missingChange}'");
                }

                throw new Exception("Some of the expected files were not modified by the version bump");
            }

            // Check if we have _extra_ changes. These might be ok, but we should verify
            var extraChanges = allChanges.Where(x => !expectedFileChanges.Contains(x)).ToList();

            var sb = new StringBuilder();
            if (extraChanges.Any())
            {
                sb.AppendLine("The following files were found to be modified. Confirm that these changes were expected " +
                              "(for example, changes to files in the MSI project are expected if our dependencies have changed).");
                sb.AppendLine();
                foreach (var extraChange in extraChanges)
                {
                    sb.Append("- [ ] ").AppendLine(extraChange);
                }

                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine("The following files were found to be modified (as expected)");
            sb.AppendLine();
            foreach (var expectedFileChange in expectedFileChanges)
            {
                sb.Append("- [x] ").AppendLine(expectedFileChange);
            }

            sb.AppendLine();
            sb.AppendLine("@DataDog/apm-dotnet");

            // need to encode the notes for use by github actions
            // see https://trstringer.com/github-actions-multiline-strings/
            sb.Replace("%","%25");
            sb.Replace("\n","%0A");
            sb.Replace("\r","%0D");

            Console.WriteLine("::set-output name=release_notes::" + sb.ToString());
        });

    Target UpdateChangeLog => _ => _
       .Unlisted()
       .Requires(() => Version)
       .Executes(() =>
        {
            var releaseNotes = Environment.GetEnvironmentVariable("RELEASE_NOTES");
            if (string.IsNullOrEmpty(releaseNotes))
            {
                Logger.Error("::error::Release notes were empty");
                throw new Exception("Release notes were empty");
            }

            Console.WriteLine("Updating changelog...");

            releaseNotes = releaseNotes.TrimEnd('\n');
            var changelogPath = RootDirectory / "docs" / "CHANGELOG.md";
            var changelog = File.ReadAllText(changelogPath);

            // find first header
            var firstHeaderIndex = changelog.IndexOf("##");

            using (var file = new StreamWriter(changelogPath, append: false))
            {
                // write the header
                file.Write(changelog.AsSpan(0, firstHeaderIndex));

                // Write the new entry
                file.WriteLine();
                file.WriteLine($"## [Release {FullVersion}](https://github.com/DataDog/{GitHubRepositoryName}/releases/tag/v{FullVersion})");
                file.WriteLine();
                file.WriteLine(releaseNotes);
                file.WriteLine();

                // Write the remainder
                file.Write(changelog.AsSpan(firstHeaderIndex));
            }
            Console.WriteLine("Changelog updated");
        });

    Target GenerateReleaseNotes => _ => _
       .Unlisted()
       .Requires(() => GitHubRepositoryName)
       .Requires(() => GitHubToken)
       .Requires(() => Version)
       .Executes(async () =>
        {
            const string fixes = "Fixes";
            const string buildAndTest = "Build / Test";
            const string misc = "Miscellaneous";
            const string tracer = "Tracer";
            const string ciVisibility = "CI Visibility";
            const string appSecMonitoring = "ASM";
            const string profiler = "Continuous Profiler";
            const string debugger = "Debugger";
            const string serverless = "Serverless";

            var artifactsLink = Environment.GetEnvironmentVariable("PIPELINE_ARTIFACTS_LINK");
            var nextVersion = FullVersion;

            var client = GetGitHubClient();

            var milestone = await GetOrCreateVNextMilestone(client);

            Console.WriteLine($"Fetching previous release details");

            Release previousRelease = null;
            try
            {
                previousRelease = await client.Repository.Release.GetLatest(
                    owner: GitHubRepositoryOwner,
                    name: GitHubRepositoryName);
            }
            catch (NotFoundException)
            {
                Console.WriteLine($"No previous release found");
            }

            Console.WriteLine($"Fetching Issues assigned to {milestone.Title}");
            var issues = await client.Issue.GetAllForRepository(
                             owner: GitHubRepositoryOwner,
                             name: GitHubRepositoryName,
                             new RepositoryIssueRequest
                             {
                                 Milestone = milestone.Number.ToString(),
                                 State = ItemStateFilter.Closed,
                                 SortProperty = IssueSort.Created,
                                 SortDirection = SortDirection.Ascending,
                             });

            Console.WriteLine($"Found {issues.Count} issues, building release notes.");

            var sb = new StringBuilder();

            sb.AppendLine("## Summary").AppendLine();
            sb.AppendLine("Write here any high level summary you may find relevant or delete the section.").AppendLine();

            sb.AppendLine("## Changes").AppendLine();

            var issueGroups = issues
                             .Select(CategorizeIssue)
                             .GroupBy(x => x.category)
                             .Select(issues =>
                              {
                                  var sb = new StringBuilder($"### {issues.Key}");
                                  sb.AppendLine();
                                  foreach (var issue in issues)
                                  {
                                      sb.AppendLine($"* {issue.issue.Title} (#{issue.issue.Number})");
                                  }

                                  return (order: CategoryToOrder(issues.Key), content: sb.ToString());
                              })
                             .OrderBy(x => x.order)
                             .Select(x => x.content);

            foreach (var issueGroup in issueGroups)
            {
                sb.AppendLine(issueGroup);
            }

            if (previousRelease is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"[Changes since {previousRelease.Name}](https://github.com/DataDog/{GitHubRepositoryName}/compare/v{previousRelease.Name}...v{nextVersion})");
            }

            // need to encode the release notes for use by github actions
            // see https://trstringer.com/github-actions-multiline-strings/
            sb.Replace("%","%25");
            sb.Replace("\n","%0A");
            sb.Replace("\r","%0D");

            Console.WriteLine("::set-output name=release_notes::" + sb.ToString());

            Console.WriteLine("Release notes generated");

            static (string category, Issue issue) CategorizeIssue(Issue issue)
            {
                var fixIssues = new[] { "type:bug", "type:regression", "type:cleanup" };
                var areaLabelToComponentMap = new Dictionary<string, string>() {
                    { "area:tracer", tracer },
                    { "area:ci-visibility", ciVisibility },
                    { "area:asm", appSecMonitoring },
                    { "area:profiler", profiler },
                    { "area:debugger", debugger },
                    { "area:serverless", serverless }
                };

                var buildAndTestIssues = new []
                {
                    "area:builds",
                    "area:benchmarks",
                    "area:benchmarks",
                    "area:tests",
                    "area:customer-samples",
                    "area:samples-and-test-apps",
                    "area:third-party-test-suites",
                    "area:test-apps",
                    "area:tools",
                    "area:vendors",
                };

                foreach((string area, string component) in areaLabelToComponentMap)
                {
                    if (issue.Labels.Any(x => x.Name == area))
                    {
                        return (component, issue);
                    }
                }

                if (issue.Labels.Any(x => fixIssues.Contains(x.Name)))
                {
                    return (fixes, issue);
                }

                if (issue.Labels.Any(x => buildAndTestIssues.Contains(x.Name)))
                {
                    return (buildAndTest, issue);
                }

                return (misc, issue);
            }

            static int CategoryToOrder(string category) => category switch
            {
                tracer => 0,
                ciVisibility => 1,
                appSecMonitoring => 2,
                profiler => 3,
                debugger => 4,
                serverless => 5,
                fixes => 6,
                _ => 7
            };
        });

    Target DownloadAzurePipelineFromBuild => _ => _
        .Unlisted()
        .Description("Downloads the release artifacts from the specified Azure DevOps BuildId")
        .DependsOn(CreateRequiredDirectories)
        .Requires(() => AzureDevopsToken)
        .Requires(() => Version)
        .Requires(() => AzureDevopsBuildId)
        .Executes(async () =>
        {
            // Connect to Azure DevOps Services
            var connection = new VssConnection(
                new Uri(AzureDevopsOrganisation),
                new VssBasicCredential(string.Empty, AzureDevopsToken));

            // Get an Azure devops client
            using var buildHttpClient = connection.GetClient<BuildHttpClient>();

            BuildArtifact artifact = await DownloadArtifactsFromConsolidatedPipelineBuild(buildHttpClient, AzureDevopsBuildId.Value, $"{FullVersion}-release-artifacts");

            var resourceDownloadUrl = artifact.Resource.DownloadUrl;

            Console.WriteLine("::set-output name=artifacts_path::" + OutputDirectory / artifact.Name);
        });

    Target DownloadReleaseArtifacts => _ => _
       .Unlisted()
       .Description("Downloads the latest artifacts from Azure Devops and Gitlab that has the provided version")
       .DependsOn(CreateRequiredDirectories)
       .Requires(() => AzureDevopsToken)
       .Requires(() => Version)
       .Requires(() => TargetBranch)
       .Executes(async () =>
       {
            // Connect to Azure DevOps Services
            var connection = new VssConnection(
                new Uri(AzureDevopsOrganisation),
                new VssBasicCredential(string.Empty, AzureDevopsToken));

            // Get an Azure devops client
            using var buildHttpClient = connection.GetClient<BuildHttpClient>();

            int buildId = await GetConsolidatedPipelineBuildId(buildHttpClient, TargetBranch, CommitSha);
            BuildArtifact artifact = await DownloadArtifactsFromConsolidatedPipelineBuild(buildHttpClient, buildId, $"{FullVersion}-release-artifacts");

            var resourceDownloadUrl = artifact.Resource.DownloadUrl;

            var artifactsPath = OutputDirectory / artifact.Name;
            Console.WriteLine("::set-output name=artifacts_link::" + resourceDownloadUrl);
            Console.WriteLine("::set-output name=artifacts_path::" + artifactsPath);

            var gitlabPath = OutputDirectory / CommitSha;
            await DownloadGitlabArtifacts(OutputDirectory, CommitSha, FullVersion);
            Console.WriteLine("::set-output name=gitlab_artifacts_path::" + gitlabPath);

            var files = artifactsPath.GlobFiles("*.*")
                .Concat(gitlabPath.GlobFiles("*.*"))
                .OrderBy(x => Path.GetFileName(x));

            var checksums = new List<string>();
            foreach (var path in files)
            {
                using var file = File.OpenRead(path);
                var expectedBytes = 512 / 8;
                var buffer = new byte[expectedBytes];
                var bytes = await SHA512.HashDataAsync(file, buffer);
                if (bytes != expectedBytes)
                {
                    throw new Exception($"Expected to write {expectedBytes}bytes, but wrote {bytes}");
                }

                var checksumLine = $"{Convert.ToHexString(buffer)} {Path.GetFileName(path)}";
                Logger.Information(checksumLine);
                checksums.Add(checksumLine);
            }

            var checksumPath = OutputDirectory / "sha512.txt";

            // Use LF so can be read on linux
            File.WriteAllText(checksumPath, string.Join("\n", checksums));

            Console.WriteLine("::set-output name=sha_path::" + checksumPath);
        });

    Target CompareCodeCoverageReports => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
         .Requires(() => GitHubRepositoryName)
         .Requires(() => GitHubToken)
         .Executes(async () =>
          {
              var newReportdir = OutputDirectory / "CodeCoverage" / "New";
              var oldReportdir = OutputDirectory / "CodeCoverage" / "Old";

              FileSystemTasks.EnsureCleanDirectory(newReportdir);
              FileSystemTasks.EnsureCleanDirectory(oldReportdir);

              // Connect to Azure DevOps Services
              var connection = new VssConnection(
                  new Uri(AzureDevopsOrganisation),
                  new VssBasicCredential(string.Empty, AzureDevopsToken));

              // Get a GitHttpClient to talk to the Git endpoints
              using var buildHttpClient = connection.GetClient<BuildHttpClient>();

              var prNumber = int.Parse(Environment.GetEnvironmentVariable("PR_NUMBER"));
              var branch = $"refs/pull/{prNumber}/merge";
              var fixedPrefix = "Code Coverage Report_";

              var (newBuild, newArtifact) = await FindAndDownloadAzureArtifact(buildHttpClient, branch, build => $"{fixedPrefix}{build.Id}", newReportdir, buildReason: null, completedBuildsOnly: false);
              var (oldBuild, oldArtifact) = await FindAndDownloadAzureArtifact(buildHttpClient, "refs/heads/master", build => $"{fixedPrefix}{build.Id}", oldReportdir, buildReason: null);

              var oldBuildId = oldArtifact.Name.Substring(fixedPrefix.Length);
              var newBuildId = newArtifact.Name.Substring(fixedPrefix.Length);

              var oldReportPath = oldReportdir / oldArtifact.Name / $"summary{oldBuildId}" / "Cobertura.xml";
              var newReportPath = newReportdir / newArtifact.Name / $"summary{newBuildId}" / "Cobertura.xml";

              var reportOldLink = $"{AzureDevopsOrganisation}/{GitHubRepositoryName}/_build/results?buildId={oldBuildId}&view=codecoverage-tab";
              var reportNewLink = $"{AzureDevopsOrganisation}/{GitHubRepositoryName}/_build/results?buildId={newBuildId}&view=codecoverage-tab";

              var downloadOldLink = oldArtifact.Resource.DownloadUrl;
              var downloadNewLink = newArtifact.Resource.DownloadUrl;

              var oldReport = Covertura.CodeCoverage.ReadReport(oldReportPath);
              var newReport = Covertura.CodeCoverage.ReadReport(newReportPath);

              var comparison = Covertura.CodeCoverage.Compare(oldReport, newReport);
              var markdown = Covertura.CodeCoverage.RenderAsMarkdown(
                  GitHubRepositoryName,
                  comparison,
                  prNumber,
                  downloadOldLink,
                  downloadNewLink,
                  reportOldLink,
                  reportNewLink,
                  oldBuild.SourceVersion,
                  newBuild.SourceVersion);

              await ReplaceCommentInPullRequest(prNumber, "## Code Coverage Report", markdown);
          });

    Target CompareBenchmarksResults => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
         .Requires(() => GitHubRepositoryName)
         .Requires(() => GitHubToken)
         .Requires(() => BenchmarkCategory)
         .Executes(async () =>
         {
             if (!int.TryParse(Environment.GetEnvironmentVariable("PR_NUMBER"), out var prNumber))
             {
                 Logger.Warning("No PR_NUMBER variable found. Skipping benchmark comparison");
                 return;
             }

             var masterDir = BuildDataDirectory / "previous_benchmarks";
             var prDir = BuildDataDirectory / "benchmarks";

             EnsureCleanDirectory(masterDir);

             // Connect to Azure DevOps Services
             var connection = new VssConnection(
                 new Uri(AzureDevopsOrganisation),
                 new VssBasicCredential(string.Empty, AzureDevopsToken));

             using var buildHttpClient = connection.GetClient<BuildHttpClient>();
             var artifactName = string.Empty;
             switch (BenchmarkCategory)
             {
                 case  "tracer": artifactName = "benchmarks_results"; break;
                 case  "appsec": artifactName = "benchmarks_appsec_results"; break;
                 default: Logger.Warning("Unknown benchmark category {BenchmarkCategory}. Skipping comparison", BenchmarkCategory); break;
             }

             var (oldBuild, _) = await FindAndDownloadAzureArtifact(buildHttpClient, "refs/heads/master", _ => artifactName, masterDir, buildReason: null);

             if (oldBuild is null)
             {
                    Logger.Warning("Old build is null");
                    return;
             }

             var markdown = CompareBenchmarks.GetMarkdown(masterDir, prDir, prNumber, oldBuild.SourceVersion, GitHubRepositoryName, BenchmarkCategory);

             await ReplaceCommentInPullRequest(prNumber, $"## Benchmarks Report for " + BenchmarkCategory, markdown);
         });

    Target CompareThroughputResults => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
         .Requires(() => GitHubRepositoryName)
         .Requires(() => GitHubToken)
         .Executes(async () =>
         {
             var isPr = int.TryParse(Environment.GetEnvironmentVariable("PR_NUMBER"), out var prNumber);

             var testedCommit = GetCommitDetails();

             var throughputDir = BuildDataDirectory / "throughput";
             var masterDir = throughputDir / "master";
             var oldBenchmarksDir = throughputDir / "benchmarks_2_9_0";
             var latestBenchmarksDir = throughputDir / "latest_benchmarks";
             var commitDir = throughputDir / "current";

             FileSystemTasks.EnsureCleanDirectory(masterDir);
             FileSystemTasks.EnsureCleanDirectory(oldBenchmarksDir);
             FileSystemTasks.EnsureCleanDirectory(latestBenchmarksDir);

             // Connect to Azure DevOps Services
             var connection = new VssConnection(
                 new Uri(AzureDevopsOrganisation),
                 new VssBasicCredential(string.Empty, AzureDevopsToken));

             using var buildHttpClient = connection.GetClient<BuildHttpClient>();

             // Grab the comparison artifacts
             var masterBuild = await GetCrankArtifacts(buildHttpClient, "refs/heads/master", masterDir);
             var oldBenchmarkBuild = await GetCrankArtifacts(buildHttpClient, "refs/heads/benchmarks/2.9.0", oldBenchmarksDir);
             var (newBenchmarkBuild, benchmarkVersion) = await GetCrankArtifactsForLatestBenchmarkBranch(buildHttpClient, latestBenchmarksDir);

             var commitName = isPr ? $"This PR ({prNumber})" : $"This commit ({testedCommit.Substring(0, 6)})";
             var sources = new List<CrankResultSource>
             {
                 new(commitName, testedCommit, CrankSourceType.CurrentCommit, commitDir),
                 new("master", masterBuild.SourceVersion, CrankSourceType.Master, masterDir),
                 new("benchmarks/2.9.0", oldBenchmarkBuild.SourceVersion, CrankSourceType.OldBenchmark, oldBenchmarksDir),
             };

             if (newBenchmarkBuild is not null && benchmarkVersion is not null)
             {
                 sources.Add(new($"benchmarks/{benchmarkVersion}", newBenchmarkBuild.SourceVersion, CrankSourceType.LatestBenchmark, latestBenchmarksDir));
             }

             var markdown = CompareThroughput.GetMarkdown(sources);

             Logger.Information("Markdown build complete, writing report");

             // save the report so we can upload it as an atefact for prosperity
             await File.WriteAllTextAsync(throughputDir / "throughput_report.md", markdown);

             if(isPr)
             {
                 Logger.Information("Updating PR comment on GitHub");
                 await ReplaceCommentInPullRequest(prNumber, "## Throughput/Crank Report", markdown);
             }

             async Task<(Microsoft.TeamFoundation.Build.WebApi.Build, string Version)> GetCrankArtifactsForLatestBenchmarkBranch(BuildHttpClient httpClient, AbsolutePath directory)
             {
                 // current (not released version)
                 var version = new Version(Version);
                 var versionsToCheck = 3;
                 while (versionsToCheck > 0 && version.Minor > 0)
                 {
                     // only looking back across minor releases (ignoring patch etc)
                     versionsToCheck--;
                     version = new Version(version.Major, version.Minor - 1, 0);

                     try
                     {
                         var thisVersion = $"{version.Major}.{version.Minor}.0";
                         var build = await GetCrankArtifacts(httpClient, $"refs/heads/benchmarks/{thisVersion}", directory);
                         return (build, thisVersion);
                     }
                     catch (Exception)
                     {
                         // if this fails, it's because we have no primary artifacts for that branch
                         Console.WriteLine($"No artifacts found for version {version}, checking next branch");
                     }
                 }

                 Console.WriteLine("No benchmarks found, skipping");
                 return (null, null);
             }

             async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetCrankArtifacts(BuildHttpClient httpClient, string branch, AbsolutePath directory)
             {
                 // find the first build with the linux crank results
                 var (build, _) = await FindAndDownloadAzureArtifact(httpClient, branch, build => "crank_linux_x64_1", directory, buildReason: null);

                 // get all the other artifacts from the same build for consistency
                 var artifacts = new[] { "crank_linux_arm64_1", "crank_linux_x64_asm_1", "crank_windows_x64_1" };
                 foreach (var artifactName in artifacts)
                 {
                     try
                     {
                         var artifact = await httpClient.GetArtifactAsync(
                                        project: AzureDevopsProjectId,
                                        buildId: build.Id,
                                        artifactName: artifactName);
                         await DownloadAzureArtifact(directory, artifact, AzureDevopsToken);
                     }
                     catch (ArtifactNotFoundException)
                     {
                         Console.WriteLine($"Could not find {artifactName} artifact for build {build.Id}. Skipping");
                     }
                 }

                 return build;
             }
         });

    Target CompareExecutionTimeBenchmarkResults => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
         .Requires(() => GitHubRepositoryName)
         .Requires(() => GitHubToken)
         .Executes(async () =>
         {
             var isPr = int.TryParse(Environment.GetEnvironmentVariable("PR_NUMBER"), out var prNumber);
             var testedCommit = GetCommitDetails();

             var executionDir = BuildDataDirectory / "execution_benchmarks";
             var masterDir = executionDir / "master";
             var commitDir = executionDir / "current";

             FileSystemTasks.EnsureCleanDirectory(masterDir);

             // Connect to Azure DevOps Services
             var connection = new VssConnection(
                 new Uri(AzureDevopsOrganisation),
                 new VssBasicCredential(string.Empty, AzureDevopsToken));

             using var buildHttpClient = connection.GetClient<BuildHttpClient>();

             // Grab the comparison artifacts
             var masterBuild = await GetExecutionBenchmarkArtifacts(buildHttpClient, "refs/heads/master", masterDir);

             var commitName = isPr ? $"This PR ({prNumber})" : $"This commit ({testedCommit.Substring(0, 6)})";
             var sources = new List<ExecutionTimeResultSource>
             {
                 new(commitName, testedCommit, ExecutionTimeSourceType.CurrentCommit, commitDir),
                 new("master", masterBuild.SourceVersion, ExecutionTimeSourceType.Master, masterDir),
              };

             var markdown = CompareExecutionTime.GetMarkdown(sources);

             Logger.Information("Markdown build complete, writing report");

             // save the report so we can upload it as an atefact for prosperity
             await File.WriteAllTextAsync(executionDir / "execution_time_report.md", markdown);

             if(isPr)
             {
                 Logger.Information("Updating PR comment on GitHub");
                 await ReplaceCommentInPullRequest(prNumber, "## Execution-Time Benchmarks Report", markdown);
             }

             async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetExecutionBenchmarkArtifacts(BuildHttpClient httpClient, string branch, AbsolutePath directory)
             {
                 // find the first build with the execution benchmarks results
                 var (build, _) = await FindAndDownloadAzureArtifact(httpClient, branch, build => "execution_time_benchmarks_windows_x64_HttpMessageHandler_1", directory, buildReason: null);

                 // get all the other artifacts from the same build for consistency
                 var artifacts = new[] { "execution_time_benchmarks_windows_x64_FakeDbCommand_1" };
                 foreach (var artifactName in artifacts)
                 {
                     try
                     {
                         var artifact = await httpClient.GetArtifactAsync(
                                        project: AzureDevopsProjectId,
                                        buildId: build.Id,
                                        artifactName: artifactName);
                         await DownloadAzureArtifact(directory, artifact, AzureDevopsToken);
                     }
                     catch (ArtifactNotFoundException)
                     {
                         Console.WriteLine($"Could not find {artifactName} artifact for build {build.Id}. Skipping");
                     }
                 }

                 return build;
             }
         });

    Target VerifyReleaseReadiness => _ => _
            .Unlisted()
            .Requires(() => GitHubToken)
            .Requires(() => CommitSha)
            .Executes(async () =>
            {
                Logger.Information("Verifying SSI artifact build succeeded for commit {Commit}...", CommitSha);
                var client = GetGitHubClient();
                var statuses = await client.Repository.Status.GetAll(
                    owner: GitHubRepositoryOwner,
                    name: GitHubRepositoryName,
                    reference: CommitSha);

                // find all the gitlab-related SSI statuses, they _all_ need to have passed
                // (apart from the serverless one, we'll ignore that for now)
                // This includes the _full_ list, so we just want to check that we have a success for each unique job
                var ssiStatuses = statuses
                    .Where(x => x.Context.StartsWith("dd-gitlab/") && x.Context != "dd-gitlab/benchmark-serverless")
                    .ToLookup(x => x.Context, x => x);

                // System.Diagnostics.Debugger.Launch();
                if (ssiStatuses.Count == 0)
                {
                    throw new Exception("No GitLab builds for SSI artifacts found. Please check the commit and try again");
                }

                var failedSsi = ssiStatuses
                    .Where(x => !x.Any(status => status.State == CommitState.Success))
                    .ToList();

                if (failedSsi.Any())
                {
                    Logger.Warning("The following gitlab jobs did not complete successfully. Please check the builds for details about why");
                    foreach (var failed in failedSsi)
                    {
                        var build = failed.OrderBy(c => c.State.Value).First();
                        Logger.Warning("- {Job} ({Status}) {Link}", failed.Key, build.State, build.TargetUrl);
                    }
                    
                    throw new Exception("Some gitlab jobs did not build/test successfully. Please check the builds for details about why.");
                }

                var stages = string.Join(", ", ssiStatuses.Select(x => x.Key));
                Logger.Information("All gitlab build stages ({Stages}) completed successfully", stages);
                
                // assert that the docker image for the commit is present
                var image = $"ghcr.io/datadog/dd-trace-dotnet/dd-lib-dotnet-init:{CommitSha}";
                VerifyDockerImageExists(image);
                
                if(new Version(Version).Major < 3)
                {
                    image = $"{image}-musl";
                    VerifyDockerImageExists(image);
                }

                static void VerifyDockerImageExists(string image)
                {
                    try
                    {
                        Logger.Information("Checking for presence of SSI image '{Image}'", image);
                        DockerTasks.DockerManifest(
                            s => s.SetCommand($"inspect")
                                .SetProcessArgumentConfigurator(c => c.Add(image)));
                        Logger.Information("SSI image '{Image}' exists", image);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error verifying SSI artifacts: '{image}' could not be found. Ensure GitLab has successfully built and pushed the image", ex);
                    }
                }
            });

    async Task ReplaceCommentInPullRequest(int prNumber, string title, string markdown)
    {
        try
        {
            Console.WriteLine("Replacing comment in GitHub");
        
            var clientId = "nuke-ci-client";
            var productInformation = Octokit.GraphQL.ProductHeaderValue.Parse(clientId);
            var connection = new Octokit.GraphQL.Connection(productInformation, GitHubToken);

            var query = new Octokit.GraphQL.Query()
                       .Repository(GitHubRepositoryName, GitHubRepositoryOwner)
                       .PullRequest(prNumber)
                       .Comments()
                       .AllPages()
                       .Select(comment => new { comment.Id, comment.Body, comment.IsMinimized });

            var prComments = (await connection.Run(query)).ToList();
        
            Console.WriteLine($"Found {prComments.Count} comments for PR {prNumber}");

            var updated = false;
            foreach (var prComment in prComments)
            {
                if (prComment.IsMinimized || !prComment.Body.StartsWith(title))
                {
                    continue;
                }

                try
                {
                    var arg = new UpdateIssueCommentInput
                    {
                        Id = prComment.Id,
                        Body = markdown,
                        ClientMutationId = clientId
                    };

                    var mutation = new Mutation()
                                  .UpdateIssueComment(arg)
                                  .Select(x => new { x.IssueComment.Id });

                    await connection.Run(mutation);
                    updated = true;
                    Console.WriteLine($"Updated comment {prComment.Id} for PR {prNumber}");
                    break;

                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error updating comment with ID {prComment.Id}: {ex}");
                }
            }

            if (!updated)
            {
                Console.WriteLine($"No comment matching title '{title}' was found in {prNumber}, posting it for the first time.");
                await PostCommentToPullRequest(prNumber, markdown);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"There was an error trying to update comment with title '{title}': {ex}");
        }
    }

    async Task PostCommentToPullRequest(int prNumber, string markdown)
    {
        Console.WriteLine("Posting comment to GitHub");

        // post directly to GitHub as
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"token {GitHubToken}");
        httpClient.DefaultRequestHeaders.UserAgent.Add(new(new System.Net.Http.Headers.ProductHeaderValue("nuke-ci-client")));

        var url = $"https://api.github.com/repos/{GitHubRepositoryOwner}/{GitHubRepositoryName}/issues/{prNumber}/comments";
        Console.WriteLine($"Sending request to '{url}'");

        var result = await httpClient.PostAsJsonAsync(url, new { body = markdown });

        if (result.IsSuccessStatusCode)
        {
            Console.WriteLine("Comment posted successfully");
        }
        else
        {
            var response = await result.Content.ReadAsStringAsync();
            Console.WriteLine("Error: " + response);
            result.EnsureSuccessStatusCode();
        }
    }

    async Task<(Microsoft.TeamFoundation.Build.WebApi.Build, BuildArtifact)> FindAndDownloadAzureArtifact(
        BuildHttpClient buildHttpClient,
        string branch,
        Func<Microsoft.TeamFoundation.Build.WebApi.Build, string> getArtifactName,
        AbsolutePath outputDirectory,
        BuildReason? buildReason = BuildReason.IndividualCI,
        bool completedBuildsOnly = true)
    {
        var builds = await buildHttpClient.GetBuildsAsync(
                         project: AzureDevopsProjectId,
                         definitions: new[] { AzureDevopsConsolidatePipelineId },
                         reasonFilter: buildReason,
                         branchName: branch);

        if (builds?.Count == 0)
        {
            throw new Exception($"Error: could not find any builds for {branch}.");
        }

        var completedBuilds = completedBuildsOnly
                                  ? builds.Where(x => x.Status == BuildStatus.Completed).ToList()
                                  : builds;
        if (!completedBuilds.Any())
        {
            throw new Exception(
                $"Error: no completed builds for {branch} were found. " +
                $"Please wait for completion before running this workflow.");
        }

        var successfulBuilds = completedBuilds
                              .Where(x => x.Result is BuildResult.Succeeded or BuildResult.PartiallySucceeded)
                              .ToList();

        if (!successfulBuilds.Any())
        {
            // likely not critical, probably a flaky test, so just warn (and push to github actions explicitly)
            Console.WriteLine($"::warning::There were no successful builds for {branch}. Attempting to find artifacts");
        }

        Console.WriteLine($"Found {completedBuilds.Count} completed builds for {branch}. Looking for artifacts...");

        BuildArtifact artifact = null;
        Microsoft.TeamFoundation.Build.WebApi.Build artifactBuild = null;
        foreach (var build in completedBuilds.OrderByDescending(x => x.Id).ThenByDescending(x=>x.FinishTime))
        {
            var artifactName = getArtifactName(build);
            try
            {
                artifact = await buildHttpClient.GetArtifactAsync(
                               project: AzureDevopsProjectId,
                               buildId: build.Id,
                               artifactName: artifactName);
                artifactBuild = build;
                break;
            }
            catch (ArtifactNotFoundException)
            {
                Console.WriteLine($"Could not find {artifactName} artifact for build {build.Id}. Skipping");
            }
        }

        if (artifact is null)
        {
            throw new Exception($"Error: no artifacts available for {branch}");
        }

        await DownloadAzureArtifact(outputDirectory, artifact, AzureDevopsToken);
        return (artifactBuild, artifact);
    }

    static async Task DownloadAzureArtifact(AbsolutePath outputDirectory, BuildArtifact artifact, string token)
    {
        var zipPath = outputDirectory / $"{artifact.Name}.zip";

        Console.WriteLine($"Downloading artifacts from {artifact.Resource.DownloadUrl} to {zipPath}...");

        // buildHttpClient.GetArtifactContentZipAsync doesn't seem to work due to 'Redirect' response status.
        // instead of downloading resources from https://dev.azure.com/ resource url starts with https://artprodcus3.artifacts.visualstudio.com
        var temporary = new HttpClient();
        temporary.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

        var resourceDownloadUrl = artifact.Resource.DownloadUrl;
        var response = await temporary.GetAsync(resourceDownloadUrl);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error downloading artifact: {response.StatusCode}:{response.ReasonPhrase}");
        }

        await using (Stream file = File.Create(zipPath))
        {
            await response.Content.CopyToAsync(file);
        }

        Console.WriteLine($"{artifact.Name} downloaded. Extracting to {outputDirectory}...");

        UncompressZip(zipPath, outputDirectory);

        Console.WriteLine($"Artifact download complete");
    }

    static async Task DownloadGitlabArtifacts(AbsolutePath outputDirectory, string commitSha, string version)
    {
        var awsUri = $"https://dd-windowsfilter.s3.amazonaws.com/builds/tracer/{commitSha}/";
        var artifactsFiles= new []
        {
            $"{awsUri}x64/en-us/datadog-dotnet-apm-{version}-x64.msi",
            $"{awsUri}windows-native-symbols.zip",
            $"{awsUri}windows-tracer-home.zip",
        };

        var destination = outputDirectory / commitSha;
        EnsureExistingDirectory(destination);

        using var client = new HttpClient();
        foreach (var fileToDownload in artifactsFiles)
        {
            var fileName = Path.GetFileName(fileToDownload);
            var destinationFile = destination / fileName;

            Console.WriteLine($"Downloading {fileToDownload} to {destinationFile}...");
            var response = await client.GetAsync(fileToDownload);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error downloading GitLab artifacts: {response.StatusCode}:{response.ReasonPhrase}");
            }

            await using (var file = File.Create(destinationFile))
            {
                await response.Content.CopyToAsync(file);
            }
            Console.WriteLine($"{fileName} downloaded");
        }
    }

    GitHubClient GetGitHubClient() =>
        new(new ProductHeaderValue("nuke-ci-client"))
        {
            Credentials = new Credentials(GitHubToken)
        };

    private async Task<Milestone> GetOrCreateVNextMilestone(GitHubClient gitHubClient)
    {
        var milestoneName = Version switch
        {
            null or { Length: 0 } => throw new Exception("Version was unexpectedly null!"),
            { } v when v.IndexOf('.') < 0 => throw new Exception("Version didn't contain '.'!"),
            { } v => $"vNext-v{v.AsSpan(0, v.IndexOf('.'))}",
        };

        var milestone = await GetMilestone(gitHubClient, milestoneName);
        if (milestone is not null)
        {
            Console.WriteLine($"Found {milestoneName} milestone: {milestone.Number}");
            return milestone;
        }

        Console.WriteLine($"{milestoneName} milestone not found, creating");

        var milestoneRequest = new NewMilestone(milestoneName);
        milestone = await gitHubClient.Issue.Milestone.Create(
                   owner: GitHubRepositoryOwner,
                   name: GitHubRepositoryName,
                   milestoneRequest);
        Console.WriteLine($"Created {milestoneName} milestone: {milestone.Number}");
        return milestone;
    }

    private async Task<Milestone> GetMilestone(GitHubClient gitHubClient, string milestoneName)
    {
        Console.WriteLine("Fetching milestones...");
        var allOpenMilestones = await gitHubClient.Issue.Milestone.GetAllForRepository(
                                    owner: GitHubRepositoryOwner,
                                    name: GitHubRepositoryName,
                                    new MilestoneRequest { State = ItemStateFilter.Open });

        return allOpenMilestones.FirstOrDefault(x => x.Title == milestoneName);
    }

    private async Task<int> GetConsolidatedPipelineBuildId(BuildHttpClient buildHttpClient, string targetBranch, string commitSha)
    {
        // Get all the builds to TargetBranch that were triggered by a CI push
        var builds = await buildHttpClient.GetBuildsAsync(
                            project: AzureDevopsProjectId,
                            definitions: new[] { AzureDevopsConsolidatePipelineId },
                            reasonFilter: BuildReason.IndividualCI,
                            branchName: targetBranch,
                            queryOrder: BuildQueryOrder.QueueTimeDescending);

        if (builds.Count == 0)
        {
            Logger.Error($"::error::No builds found for {targetBranch}. Did you include the full git ref, e.g. refs/heads/master?");
            throw new Exception($"No builds found for {targetBranch}");
        }

        if (!string.IsNullOrEmpty(commitSha))
        {
            var foundSha = false;
            var maxCommitsBack = 20;
            // basic verification, to ensure that the provided commitsha is actually on this branch
            for (var i = 0; i < maxCommitsBack; i++)
            {
                var sha = GitTasks.Git($"log {TargetBranch}~{i} -1 --pretty=%H")
                        .FirstOrDefault(x => x.Type == OutputType.Std)
                        .Text;

                if (string.Equals(commitSha, sha, StringComparison.OrdinalIgnoreCase))
                {
                    // OK, this SHA is definitely on this branch
                    foundSha = true;
                    break;
                }
            }

            if (!foundSha)
            {
                Logger.Error($"Error: The commit {commitSha} could not be found in the last {maxCommitsBack} of the branch {TargetBranch}" +
                                $"Ensure that the commit sha you have provided is correct, and you are running the create_release action from the correct branch");
                throw new Exception($"The commit {commitSha} could not found in the latest {maxCommitsBack} of target branch {TargetBranch}");
            }


            Logger.Information($"Finding build for commit sha: {commitSha}");
            var build = builds
                .FirstOrDefault(b => string.Equals(b.SourceVersion, commitSha, StringComparison.OrdinalIgnoreCase));
            if (build is null)
            {
                throw new Exception($"No builds for commit {commitSha} found. Please check you have provided the correct SHA, and that there is a build in AzureDevops for the commit");
            }

            return build.Id;
        }
        else
        {
            // start from the current commit, and keep looking backwards until we find a commit that has a build
            // that has successful artifacts. Should only be called from branches with a linear history (i.e. single parent)
            // This solves a potential issue where we previously selecting a build by start order, not by the actual
            // git commit order. Generally that shouldn't be an issue, but if we manually trigger builds on master
            // (which we sometimes do e.g. trying to bisect and issue, or retrying flaky test for coverage reasons),
            // then we could end up selecting the wrong build.
            const int maxCommitsBack = 20;
            for (var i = 0; i < maxCommitsBack; i++)
            {
                commitSha = GitTasks.Git($"log {TargetBranch}~{i} -1 --pretty=%H")
                                    .FirstOrDefault(x => x.Type == OutputType.Std)
                                    .Text;

                Logger.Information($"Looking for builds for {commitSha}");

                foreach (var build in builds)
                {
                    if (string.Equals(build.SourceVersion, commitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found a build for the commit, so should be successful and have an artifact
                        if (build.Result != BuildResult.Succeeded && build.Result != BuildResult.PartiallySucceeded)
                        {
                            Logger.Error($"::error::The build for commit {commitSha} was not successful. Please retry any failed stages for the build before creating a release");
                            throw new Exception("Latest build for branch was not successful. Please retry the build before creating a release");
                        }

                        return build.Id;
                    }
                }
            }

            throw new Exception($"No builds for commit {CommitSha} found. Please check you have provided the correct SHA, and that there is a build in AzureDevops for the commit");
        }
    }

    private async Task<BuildArtifact> DownloadArtifactsFromConsolidatedPipelineBuild(BuildHttpClient buildHttpClient, int buildId, string artifactName)
    {
        try
        {
            BuildArtifact artifact = await buildHttpClient.GetArtifactAsync(
                            project: AzureDevopsProjectId,
                            buildId: buildId,
                            artifactName: artifactName);

            Logger.Information("Release artifacts found, downloading...");
            await DownloadAzureArtifact(OutputDirectory, artifact, AzureDevopsToken);

            return artifact;
        }
        catch (ArtifactNotFoundException)
        {
            Logger.Error($"Error: The build {buildId} for commit could not find {artifactName} artifact for build {buildId} for commit {CommitSha}. " +
                            $"Ensure the build has successfully generated artifacts for this commit before creating a release");
            throw;
        }
    }

    static string GetCommitDetails()
    {
        var testedCommit = Environment.GetEnvironmentVariable("OriginalCommitId");
        if (string.IsNullOrEmpty(testedCommit))
        {
            testedCommit = GitTasks.Git($"rev-parse HEAD").FirstOrDefault().Text;
            if(string.IsNullOrEmpty(testedCommit))
            {
                Logger.Warning("No OriginalCommitId variable found and unable to infer commit. Skipping throughput comparison");
                return null;
            }
            else
            {
                Logger.Information($"No OriginalCommitId variable found. Using inferred commit {testedCommit}");
            }
        }

        return testedCommit;
    }

    class LabbelerConfiguration
    {
        public Label[] Labels { get; set; }

        public class Label
        {
            public string Name { get; set; }
            public string Title { get; set; }
            public string AllFilesIn { get; set; }
        }
    }
}
