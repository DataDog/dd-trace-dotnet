using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Octokit;
using static Nuke.Common.IO.CompressionTasks;
using Issue = Octokit.Issue;
using Target = Nuke.Common.Target;

partial class Build
{
    [Parameter("A GitHub token (for use in GitHub Actions)", Name = "GITHUB_TOKEN")]
    readonly string GitHubToken;

    [Parameter("An Azure Devops PAT (for use in GitHub Actions)", Name = "AZURE_DEVOPS_TOKEN")]
    readonly string AzureDevopsToken;

    [Parameter("The Pull Request number for GitHub Actions")]
    readonly int? PullRequestNumber;

    const string GitHubNextMilestoneName = "vNext";
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

            Console.WriteLine($"Updating {GitHubNextMilestoneName} to {FullVersion}");

            await client.Issue.Milestone.Update(
                owner: GitHubRepositoryOwner,
                name: GitHubRepositoryName,
                number: milestone.Number,
                new MilestoneUpdate { Title = FullVersion });

            Console.WriteLine($"Milestone renamed");
            // set the output variable
            Console.WriteLine("::set-output name=milestone::" + milestone.Number);
        });

    Target OutputCurrentVersionToGitHub => _ => _
       .Unlisted()
       .Requires(() => Version)
       .Executes(() =>
        {
            Console.WriteLine("::set-output name=version::" + Version);
            Console.WriteLine("::set-output name=full_version::" + FullVersion);
            Console.WriteLine("::set-output name=isprerelease::" + (IsPrerelease ? "true" : "false"));
        });


    Target VerifyChangedFilesFromVersionBump => _ => _
       .Unlisted()
       .Description("Verifies that the expected files were changed")
       .After(UpdateVersion, UpdateMsiContents, UpdateIntegrationsJson, UpdateChangeLog)
       .Executes(() =>
        {
            var expectedFileChanges = new []
            {
                "docs/CHANGELOG.md",
                "build/_build/Build.cs",
                "integrations.json",
                "samples/AutomaticTraceIdInjection/Log4NetExample/Log4NetExample.csproj",
                "samples/AutomaticTraceIdInjection/NLog40Example/NLog40Example.csproj",
                "samples/AutomaticTraceIdInjection/NLog45Example/NLog45Example.csproj",
                "samples/AutomaticTraceIdInjection/NLog46Example/NLog46Example.csproj",
                "samples/AutomaticTraceIdInjection/SerilogExample/SerilogExample.csproj",
                "samples/ConsoleApp/Alpine3.10.dockerfile",
                "samples/ConsoleApp/Alpine3.9.dockerfile",
                "samples/ConsoleApp/Debian.dockerfile",
                "samples/WindowsContainer/Dockerfile",
                "src/Datadog.Monitoring.Distribution/Datadog.Monitoring.Distribution.csproj",
                "src/Datadog.Trace.AspNet/Datadog.Trace.AspNet.csproj",
                "src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj",
                "src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                "src/Datadog.Trace.ClrProfiler.Native/CMakeLists.txt",
                "src/Datadog.Trace.ClrProfiler.Native/dd_profiler_constants.h",
                "src/Datadog.Trace.ClrProfiler.Native/Resource.rc",
                "src/Datadog.Trace.ClrProfiler.Native/version.h",
                "src/Datadog.Trace.MSBuild/Datadog.Trace.MSBuild.csproj",
                "src/Datadog.Trace.OpenTracing/Datadog.Trace.OpenTracing.csproj",
                "src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.Standalone.csproj",
                "src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.Tool.csproj",
                "src/Datadog.Trace/Datadog.Trace.csproj",
                "src/Datadog.Trace/TracerConstants.cs",
                "src/WindowsInstaller/WindowsInstaller.wixproj",
                "test/test-applications/regression/AutomapperTest/Dockerfile",
            };

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
       .Requires(() => GitHubToken)
       .Requires(() => Version)
       .Executes(async () =>
        {
            const string fixes = "Fixes";
            const string buildAndTest = "Build / Test";
            const string changes = "Changes";
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

            Console.WriteLine($"Fetching Issues assigned to {GitHubNextMilestoneName}");
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

            Console.WriteLine($"Found {issues.Count} issues, building changelog");

            var sb = new StringBuilder();
            sb.AppendLine($"## [Release {nextVersion}](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v{nextVersion})");
            sb.AppendLine();

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

            Console.WriteLine("Updating changelog...");

            // Not very performant, but it'll do for now

            var changelogPath = RootDirectory / "docs" / "CHANGELOG.md";
            var changelog = File.ReadAllText(changelogPath);

            // find first header
            var firstHeader = changelog.IndexOf("##");

            using (var file = new StreamWriter(changelogPath, append: false))
            {
                // write the header
                file.Write(changelog.AsSpan(0, firstHeader));

                // Write the new entry
                file.Write(sb.ToString());

                // Write the remainder
                file.Write(changelog.AsSpan(firstHeader));
            }

            Console.WriteLine("Changelog updated");

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

    Target ExtractReleaseNotes => _ => _
      .Unlisted()
      .Executes(() =>
       {
           Console.WriteLine("Reading changelog...");

           var changelogPath = RootDirectory / "docs" / "CHANGELOG.md";
           var changelog = File.ReadAllText(changelogPath);

           // find first header
           var releaseNotesStart = changelog.IndexOf($"## [Release {FullVersion}]");
           var firstContent = changelog.IndexOf("##", startIndex: releaseNotesStart + 3);
           var releaseNotesEnd = changelog.IndexOf($"## [Release", firstContent);

           var artifactsLink = Environment.GetEnvironmentVariable("PIPELINE_ARTIFACTS_LINK");

           var sb = new StringBuilder();
           sb.AppendLine($"⚠ 1. Download the NuGet packages for the release from [this link]({artifactsLink}) and upload to nuget.org");
           sb.AppendLine("⚠ 2. Download the signed MSI assets from GitLab and attach to this release before publishing");
           sb.AppendLine();
           sb.Append(changelog, firstContent, releaseNotesEnd - firstContent);

           Console.WriteLine(sb.ToString());

           // need to encode the release notes for use by github actions
           // see https://trstringer.com/github-actions-multiline-strings/
           sb.Replace("%","%25");
           sb.Replace("\n","%0A");
           sb.Replace("\r","%0D");

           Console.WriteLine("::set-output name=release_notes::" + sb.ToString());

           Console.WriteLine("Release notes generated");
       });


    Target DownloadAzurePipelineArtifacts => _ => _
       .Unlisted()
       .DependsOn(CreateRequiredDirectories)
       .Requires(() => AzureDevopsToken)
       .Requires(() => Version)
       .Executes(async () =>
       {
            // Connect to Azure DevOps Services
            var connection = new VssConnection(
                new Uri(AzureDevopsOrganisation),
                new VssBasicCredential(string.Empty, AzureDevopsToken));

            // Get a GitHttpClient to talk to the Git endpoints
            using var buildHttpClient = connection.GetClient<BuildHttpClient>();

            var branch = $"refs/tags/v{FullVersion}";

            var builds = await buildHttpClient.GetBuildsAsync(
                              project: AzureDevopsProjectId,
                              definitions: new[] { AzureDevopsConsolidatePipelineId },
                              reasonFilter: BuildReason.IndividualCI,
                              branchName: branch);

            if (builds?.Count == 0)
            {
                throw new Exception($"Error: could not find any builds for {branch}. " +
                                    $"Are you sure you've merged the version bump PR?");
            }

            var completedBuilds = builds
                                 .Where(x => x.Status == BuildStatus.Completed)
                                 .ToList();
            if (!completedBuilds.Any())
            {
                throw new Exception($"Error: no completed builds for {branch} were found. " +
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

            var artifactName = $"{FullVersion}-release-artifacts";

            BuildArtifact artifact = null;
            foreach (var build in completedBuilds.OrderByDescending(x => x.FinishTime)) // Successful builds
            {
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
                    Console.WriteLine($"Could not find {artifactName} artifact for build {build.Id}. Skipping");
                }
            }

            if (artifact is null)
            {
                throw new Exception($"Error: no artifacts available for {branch}");
            }

            var zipPath = OutputDirectory / $"{artifactName}.zip";

            Console.WriteLine($"Found artifacts. Downloading to {zipPath}...");

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

            Console.WriteLine($"{artifactName} downloaded. Extracting to {OutputDirectory}...");

            UncompressZip(zipPath, OutputDirectory);

            Console.WriteLine($"Artifact download complete");
            Console.WriteLine("::set-output name=artifacts_link::" + resourceDownloadUrl);
            Console.WriteLine("::set-output name=artifacts_path::" + OutputDirectory / artifactName);
        });

    GitHubClient GetGitHubClient() =>
        new(new ProductHeaderValue("nuke-ci-client"))
        {
            Credentials = new Credentials(GitHubToken)
        };

    private static async Task<Milestone> GetOrCreateVNextMilestone(GitHubClient gitHubClient)
    {
        Console.WriteLine("Fetching milestones...");
        var allOpenMilestones = await gitHubClient.Issue.Milestone.GetAllForRepository(
                                    owner: GitHubRepositoryOwner,
                                    name: GitHubRepositoryName,
                                    new MilestoneRequest { State = ItemStateFilter.Open });

        var milestone = allOpenMilestones.FirstOrDefault(x => x.Title == GitHubNextMilestoneName);
        if (milestone is not null)
        {
            Console.WriteLine($"Found {GitHubNextMilestoneName} milestone: {milestone.Number}");
            return milestone;
        }

        Console.WriteLine($"{GitHubNextMilestoneName} milestone not found, creating");

        var milestoneRequest = new NewMilestone(GitHubNextMilestoneName);
        milestone = await gitHubClient.Issue.Milestone.Create(
                   owner: GitHubRepositoryOwner,
                   name: GitHubRepositoryName,
                   milestoneRequest);
        Console.WriteLine($"Created {GitHubNextMilestoneName} milestone: {milestone.Number}");
        return milestone;
    }

}
