using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using BenchmarkComparison;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using Issue = Octokit.Issue;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Target = Nuke.Common.Target;
using static Octokit.GraphQL.Variable;
using Environment = System.Environment;
using Milestone = Octokit.Milestone;
using Release = Octokit.Release;

partial class Build
{
    [Parameter("A GitHub token (for use in GitHub Actions)", Name = "GITHUB_TOKEN")]
    readonly string GitHubToken;

    [Parameter("An Azure Devops PAT (for use in GitHub Actions)", Name = "AZURE_DEVOPS_TOKEN")]
    readonly string AzureDevopsToken;

    [Parameter("The Pull Request number for GitHub Actions")]
    readonly int? PullRequestNumber;

    [Parameter("The git branch to use", List = false)]
    readonly string TargetBranch;

    [Parameter("Is the ChangeLog expected to change?", List = false)]
    readonly bool ExpectChangelogUpdate = true;

    const string GitHubRepositoryOwner = "DataDog";
    const string GitHubRepositoryName = "dd-trace-dotnet";
    const string AzureDevopsOrganisation = "https://dev.azure.com/datadoghq";
    const int AzureDevopsConsolidatePipelineId = 54;
    static readonly Guid AzureDevopsProjectId = Guid.Parse("a51c4863-3eb4-4c5d-878a-58b41a049e4e");

    string FullVersion => IsPrerelease ? $"{Version}-prerelease" : Version;

    Target AssignPullRequestToMilestone => _ => _
       .Unlisted()
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

    Target RenameVNextMilestone => _ => _
       .Unlisted()
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
       .After(UpdateVersion, UpdateMsiContents)
       .Requires(() => Version)
       .Executes(() =>
        {
            Console.WriteLine("Using version to " + FullVersion);
            Console.WriteLine("::set-output name=version::" + Version);
            Console.WriteLine("::set-output name=full_version::" + FullVersion);
            Console.WriteLine("::set-output name=isprerelease::" + (IsPrerelease ? "true" : "false"));
        });

    Target CalculateNextVersion => _ => _
       .Unlisted()
       .Requires(() => Version)
       .Executes(() =>
        {
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
            Console.WriteLine("::set-output name=isprerelease::false");
        });

    Target VerifyChangedFilesFromVersionBump => _ => _
       .Unlisted()
       .Description("Verifies that the expected files were changed")
       .After(UpdateVersion, UpdateMsiContents, UpdateChangeLog)
       .Executes(() =>
        {
            var expectedFileChanges = new List<string>
            {
                "shared/src/msi-installer/WindowsInstaller.wixproj",
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
                "tracer/samples/WindowsContainer/Dockerfile",
                "tracer/src/Datadog.Monitoring.Distribution/Datadog.Monitoring.Distribution.csproj",
                "tracer/src/Datadog.Trace.AspNet/Datadog.Trace.AspNet.csproj",
                "tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj",
                "tracer/src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                "tracer/src/Datadog.Trace.ClrProfiler.Native/CMakeLists.txt",
                "tracer/src/Datadog.Trace.ClrProfiler.Native/dd_profiler_constants.h",
                "tracer/src/Datadog.Trace.ClrProfiler.Native/Resource.rc",
                "tracer/src/Datadog.Trace.ClrProfiler.Native/version.h",
                "tracer/src/Datadog.Trace.MSBuild/Datadog.Trace.MSBuild.csproj",
                "tracer/src/Datadog.Trace.OpenTracing/Datadog.Trace.OpenTracing.csproj",
                "tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj",
                "tracer/src/Datadog.Trace/Datadog.Trace.csproj",
                "tracer/src/Datadog.Trace/TracerConstants.cs",
                "tracer/src/WindowsInstaller/WindowsInstaller.wixproj",
                "tracer/test/test-applications/regression/AutomapperTest/Dockerfile",
            };

            if (ExpectChangelogUpdate)
            {
                expectedFileChanges.Insert(0, "docs/CHANGELOG.md");
            }

            Logger.Info("Verifying that all expected files changed...");
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
                file.WriteLine($"## [Release {FullVersion}](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v{FullVersion})");
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
       .Requires(() => GitHubToken)
       .Requires(() => Version)
       .Executes(async () =>
        {
            const string fixes = "Fixes";
            const string buildAndTest = "Build / Test";
            const string changes = "Changes";
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

            var issueGroups = issues
                             .Select(CategoriseIssue)
                             .GroupBy(x => x.category)
                             .Select(issues =>
                              {
                                  var sb = new StringBuilder($"## {issues.Key}");
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
                sb.AppendLine($"[Changes since {previousRelease.Name}](https://github.com/DataDog/dd-trace-dotnet/compare/v{previousRelease.Name}...v{nextVersion})")
                  .AppendLine();
            }

            // need to encode the release notes for use by github actions
            // see https://trstringer.com/github-actions-multiline-strings/
            sb.Replace("%","%25");
            sb.Replace("\n","%0A");
            sb.Replace("\r","%0D");

            Console.WriteLine("::set-output name=release_notes::" + sb.ToString());

            Console.WriteLine("Release notes generated");

            static (string category, Issue issue) CategoriseIssue(Issue issue)
            {
                var fixIssues = new[] { "type:bug", "type:regression", "type:cleanup" };
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

                if (issue.Labels.Any(x => fixIssues.Contains(x.Name)))
                {
                    return (fixes, issue);
                }
                if (issue.Labels.Any(x => buildAndTestIssues.Contains(x.Name)))
                {
                    return (buildAndTest, issue);
                }

                return (changes, issue);
            }

            static int CategoryToOrder(string category) => category switch
            {
                changes => 0,
                fixes => 1,
                _ => 2
            };
        });

    Target DownloadAzurePipelineAndGitlabArtifacts => _ => _
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

            // Get all the builds to TargetBranch that were triggered by a CI push

            var builds = await buildHttpClient.GetBuildsAsync(
                             project: AzureDevopsProjectId,
                             definitions: new[] { AzureDevopsConsolidatePipelineId },
                             reasonFilter: BuildReason.IndividualCI,
                             branchName: TargetBranch,
                             queryOrder: BuildQueryOrder.QueueTimeDescending);

            if (builds.Count == 0)
            {
                Logger.Error($"::error::No builds found for {TargetBranch}. Did you include the full git ref, e.g. refs/heads/master?");
                throw new Exception($"No builds found for {TargetBranch}");
            }

            BuildArtifact artifact = null;
            var artifactName = $"{FullVersion}-release-artifacts";

            Logger.Info($"Checking builds for artifact called: {artifactName}");
            string commitSha = String.Empty;

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

                Logger.Info($"Looking for builds for {commitSha}");

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

                        try
                        {
                            artifact = await buildHttpClient.GetArtifactAsync(
                                           project: AzureDevopsProjectId,
                                           buildId: build.Id,
                                           artifactName: artifactName);

                            break;
                        }
                        catch (ArtifactNotFoundException)
                        {
                            Logger.Error($"Error: could not find {artifactName} artifact for build {build.Id} for commit {commitSha}. " +
                                         $"Ensure the build has completed successfully for this commit before creating a release");
                            throw;
                        }
                    }
                }

                if (artifact is not null)
                {
                    break;
                }
            }

            if (artifact is null)
            {
                Logger.Error($"::error::Could not find artifacts called {artifactName} for release. Please ensure the pipeline is running correctly for commits to this branch");
                throw new Exception("Could not find any artifacts to create a release");
            }

            Logger.Info("Release artifacts found, downloading...");

            await DownloadAzureArtifact(OutputDirectory, artifact);
            var resourceDownloadUrl = artifact.Resource.DownloadUrl;

            Console.WriteLine("::set-output name=artifacts_link::" + resourceDownloadUrl);
            Console.WriteLine("::set-output name=artifacts_path::" + OutputDirectory / artifact.Name);
            
            await DownloadGitlabArtifacts(OutputDirectory, commitSha, FullVersion);
            Console.WriteLine("::set-output name=gitlab_artifacts_path::" + OutputDirectory / commitSha);
        });

    Target CompareCodeCoverageReports => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
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

              var reportOldLink = $"{AzureDevopsOrganisation}/dd-trace-dotnet/_build/results?buildId={oldBuildId}&view=codecoverage-tab";
              var reportNewLink = $"{AzureDevopsOrganisation}/dd-trace-dotnet/_build/results?buildId={newBuildId}&view=codecoverage-tab";

              var downloadOldLink = oldArtifact.Resource.DownloadUrl;
              var downloadNewLink = newArtifact.Resource.DownloadUrl;

              var oldReport = Covertura.CodeCoverage.ReadReport(oldReportPath);
              var newReport = Covertura.CodeCoverage.ReadReport(newReportPath);

              var comparison = Covertura.CodeCoverage.Compare(oldReport, newReport);
              var markdown = Covertura.CodeCoverage.RenderAsMarkdown(
                  comparison,
                  prNumber,
                  downloadOldLink,
                  downloadNewLink,
                  reportOldLink,
                  reportNewLink,
                  oldBuild.SourceVersion,
                  newBuild.SourceVersion);

              await HideCommentsInPullRequest(prNumber, "## Code Coverage Report");
              await PostCommentToPullRequest(prNumber, markdown);
          });

    Target CompareBenchmarksResults => _ => _
         .Unlisted()
         .DependsOn(CreateRequiredDirectories)
         .Requires(() => AzureDevopsToken)
         .Requires(() => GitHubToken)
         .Executes(async () =>
         {
             if (!int.TryParse(Environment.GetEnvironmentVariable("PR_NUMBER"), out var prNumber))
             {
                 Logger.Warn("No PR_NUMBER variable found. Skipping benchmark comparison");
                 return;
             }

             var masterDir = BuildDataDirectory / "previous_benchmarks";
             var prDir = BuildDataDirectory / "benchmarks";

             FileSystemTasks.EnsureCleanDirectory(masterDir);

             // Connect to Azure DevOps Services
             var connection = new VssConnection(
                 new Uri(AzureDevopsOrganisation),
                 new VssBasicCredential(string.Empty, AzureDevopsToken));

             using var buildHttpClient = connection.GetClient<BuildHttpClient>();

             var (oldBuild, _) = await FindAndDownloadAzureArtifact(buildHttpClient, "refs/heads/master", build => "benchmarks_results", masterDir, buildReason: null);

             var markdown = CompareBenchmarks.GetMarkdown(masterDir, prDir, prNumber, oldBuild.SourceVersion);

             await HideCommentsInPullRequest(prNumber, "## Benchmarks Report");
             await PostCommentToPullRequest(prNumber, markdown);
         });

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

    async Task HideCommentsInPullRequest(int prNumber, string prefix)
    {
        try
        {
            Console.WriteLine("Looking for comments to hide in GitHub");

            var clientId = "nuke-ci-client";
            var productInformation = Octokit.GraphQL.ProductHeaderValue.Parse(clientId);
            var connection = new Octokit.GraphQL.Connection(productInformation, GitHubToken);

            var query = new Octokit.GraphQL.Query()
                       .Repository(GitHubRepositoryName, GitHubRepositoryOwner)
                       .PullRequest(prNumber)
                       .Comments()
                       .AllPages()
                       .Select(issue => new { issue.Id, issue.Body, issue.IsMinimized, });

            var issueComments =  (await connection.Run(query)).ToList();

            Console.WriteLine($"Found {issueComments} comments for PR {prNumber}");

            var count = 0;
            foreach (var issueComment in issueComments)
            {
                if (issueComment.IsMinimized || ! issueComment.Body.StartsWith(prefix))
                {
                    continue;
                }

                try
                {
                    var arg = new MinimizeCommentInput
                    {
                        Classifier = ReportedContentClassifiers.Outdated,
                        SubjectId = issueComment.Id,
                        ClientMutationId = clientId
                    };

                    var mutation = new Mutation()
                                  .MinimizeComment(arg)
                                  .Select(x => new { x.MinimizedComment.IsMinimized });

                    await connection.Run(mutation);
                    count++;

                }
                catch (Exception ex)
                {
                    Logger.Warn($"Error minimising comment with ID {issueComment.Id}: {ex}");
                }
            }

            Console.WriteLine($"Minimised {count} comments for PR {prNumber}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"There was an error trying to minimise old comments with prefix '{prefix}': {ex}");
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
                              .Where(x => x.Result == BuildResult.Succeeded || x.Result == BuildResult.PartiallySucceeded)
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

        await DownloadAzureArtifact(outputDirectory, artifact);
        return (artifactBuild, artifact);
    }

    static async Task DownloadAzureArtifact(AbsolutePath outputDirectory, BuildArtifact artifact)
    {
        var zipPath = outputDirectory / $"{artifact.Name}.zip";

        Console.WriteLine($"Downloading artifacts from {artifact.Resource.DownloadUrl} to {zipPath}...");

        // buildHttpClient.GetArtifactContentZipAsync doesn't seem to work
        var temporary = new HttpClient();
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
            $"{awsUri}x86/en-us/datadog-dotnet-apm-{version}-x86.msi", 
            $"{awsUri}windows-native-symbols.zip"
        };

        using var client = new HttpClient();
        foreach (var fileToDownload in artifactsFiles)
        {
            var fileName = Path.GetFileName(fileToDownload);
            var destination = outputDirectory / commitSha / fileName;
            EnsureExistingDirectory(destination);

            Console.WriteLine($"Downloading {fileName} to {destination}...");
            var response = await client.GetAsync(fileToDownload);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error downloading GitLab artifacts: {response.StatusCode}:{response.ReasonPhrase}");
            }

            await using (var file = File.Create(destination))
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
        var milestoneName = Version.StartsWith("1.") ? "vNext-v1" : "vNext";

        Console.WriteLine("Fetching milestones...");
        var allOpenMilestones = await gitHubClient.Issue.Milestone.GetAllForRepository(
                                    owner: GitHubRepositoryOwner,
                                    name: GitHubRepositoryName,
                                    new MilestoneRequest { State = ItemStateFilter.Open });

        var milestone = allOpenMilestones.FirstOrDefault(x => x.Title == milestoneName);
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

}
