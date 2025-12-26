using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement.Model;
using DiffMatchPatch;
using GenerateSpanDocumentation;
using GeneratePackageVersions;
using Honeypot;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using PrepareRelease;
using UpdateVendors;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Target = Nuke.Common.Target;
using Logger = Serilog.Log;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Parameter("The sample name to execute when running or building sample apps")]
    readonly string SampleName;

    [Parameter("The id of a build in AzureDevops")]
    readonly string BuildId;

    [Parameter("Additional environment variables, in the format KEY1=Value1 Key2=Value2 to use when running the IIS Sample")]
    readonly string[] ExtraEnvVars;

    [Parameter("Force ARM64 build in Windows")]
    readonly bool ForceARM64BuildInWindows;

    [Parameter("Don't update package versions for packages with the following names")]
    readonly string[] ExcludePackages;

    [Parameter("Only update package versions for packages with the following names")]
    readonly string[] IncludePackages;

    [LazyLocalExecutable(@"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\gacutil.exe")]
    readonly Lazy<Tool> GacUtil;
    [LazyLocalExecutable(@"C:\Program Files\IIS Express\iisexpress.exe")]
    readonly Lazy<Tool> IisExpress;

    [Parameter("The (overridden) API version to use when building sample projects (e.g. '4.7.1')")]
    readonly string ApiVersion;

    AbsolutePath IisExpressApplicationConfig =>
        RootDirectory / ".vs" / Solution.Name / "config" / "applicationhost.config";

    readonly IEnumerable<string> GacProjects = new []
    {
        Projects.DatadogTrace,
    };

    Target GacAdd => _ => _
        .Description("Adds the (already built) files to the Windows GAC **REQUIRES ELEVATED PERMISSIONS** ")
        .Requires(() => IsWin)
        .DependsOn(GacRemove)
        .After(BuildTracerHome)
        .Requires(() => Framework)
        .Executes(() =>
        {
            foreach (var dll in GacProjects)
            {
                var path = MonitoringHomeDirectory / Framework / $"{dll}.dll";
                GacUtil.Value($"/i \"{path}\"");
            }
        });

    Target GacRemove => _ => _
        .Description("Removes the Datadog tracer files from the Windows GAC **REQUIRES ELEVATED PERMISSIONS** ")
        .Requires(() => IsWin)
        .Executes(() =>
        {
            foreach (var dll in GacProjects)
            {
                GacUtil.Value($"/u \"{dll}\"");
            }
        });

    Target RunInstrumentationGenerator => _ => _
       .Description("Runs the AutoInstrumentation Generator")
       .Executes(() =>
       {
           var autoInstGenProj =
               SourceDirectory / "Datadog.AutoInstrumentation.Generator" / "Datadog.AutoInstrumentation.Generator.csproj";

           // We make sure the autoinstrumentation generator builds so we can fail the task if not.
           DotNetRestore(s => s
                             .SetDotnetPath(TargetPlatform)
                             .SetProjectFile(autoInstGenProj)
                             .SetNoWarnDotNetCore3());

           DotNetBuild(s => s
                           .SetDotnetPath(TargetPlatform)
                           .SetFramework(TargetFramework.NET7_0)
                           .SetProjectFile(autoInstGenProj)
                           .SetConfiguration(Configuration.Release)
                           .SetNoWarnDotNetCore3());

           // We need to run the generator this way to avoid nuke waiting until the process finishes.
           var dotnetRunSettings = new DotNetRunSettings()
                                  .SetDotnetPath(TargetPlatform)
                                  .SetNoBuild(true)
                                  .SetFramework(TargetFramework.NET7_0)
                                  .EnableNoLaunchProfile()
                                  .SetProjectFile(autoInstGenProj)
                                  .SetConfiguration(Configuration.Release);
           ProcessTasks.StartProcess(dotnetRunSettings);
       });

    Target BuildIisSampleApp => _ => _
        .Description("Rebuilds an IIS sample app")
        .Requires(() => SampleName)
        .Requires(() => Solution.GetProject(SampleName) != null)
        .Executes(() =>
        {
            MSBuild(s => s
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .SetProjectFile(Solution.GetProject(SampleName)));
        });

    Target RunIisSample => _ => _
        .Description("Runs an IIS sample app, enabling profiling.")
        .Requires(() => SampleName)
        .Requires(() => IsWin)
        .Executes(() =>
        {
            var envVars = new Dictionary<string,string>(new ProcessStartInfo().Environment);

            AddTracerEnvironmentVariables(envVars);
            AddExtraEnvVariables(envVars, ExtraEnvVars);

            Logger.Information($"Running sample '{SampleName}' in IIS Express");
            IisExpress.Value(
                arguments: $"/config:\"{IisExpressApplicationConfig}\" /site:{SampleName} /appPool:Clr4IntegratedAppPool",
                environmentVariables: envVars);
        });

    Target RunDotNetSample => _ => _
        .Description("Builds and runs a sample app using dotnet run, enabling profiling.")
        .Requires(() => SampleName)
        .Requires(() => Framework)
        .Executes(() => {

            var envVars = new Dictionary<string, string> { { "ASPNETCORE_URLS", "http://*:5003" } };
            AddTracerEnvironmentVariables(envVars);
            //we're not supplying a dll file so process will be independent and wont be hosted by dotnet
            envVars.Add("DD_PROFILER_EXCLUDE_PROCESSES", "dotnet.exe");
            AddExtraEnvVariables(envVars, ExtraEnvVars);

            string project = Solution.GetProject(SampleName)?.Path;
            if (project is not null)
            {
                Logger.Information($"Running sample '{SampleName}'");
            }
            else if (System.IO.File.Exists(SampleName))
            {
                project = SampleName;
                Logger.Information($"Running project '{SampleName}'");
            }
            else
            {
                throw new Exception($"Could not find project in solution with name '{SampleName}', " +
                                                    "and no project file with the provided path exists");
            }

            DotNetBuild(s => s
                .SetFramework(Framework)
                .SetProjectFile(project)
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetProperty("platform", TargetPlatform));

            DotNetRun(s => s
                .SetDotnetPath(TargetPlatform)
                .SetFramework(Framework)
                .EnableNoLaunchProfile()
                .SetProjectFile(project)
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetProperty("platform", TargetPlatform)
                .SetProcessEnvironmentVariables(envVars));

        });

    Target GeneratePackageVersions => _ => _
       .Description("Regenerate the PackageVersions props and .cs files")
       .DependsOn(Clean, Restore, CreateRequiredDirectories, CompileManagedSrc, PublishManagedTracer)
       .Executes(async () =>
       {
           if (IncludePackages is not null)
           {
               Logger.Information("Including only NuGet packages matching {PackageNames}", string.Join(", ", IncludePackages));
           }
           if (ExcludePackages is not null)
           {
               Logger.Information("Excluding NuGet packages matching {PackageNames}", string.Join(", ", ExcludePackages));
           }

           if (IncludePackages is not null && ExcludePackages is not null)
           {
               throw new ArgumentException("You cannot specify IncludePackages AND ExcludePackages");
           }

           var testDir = Solution.GetProject(Projects.ClrProfilerIntegrationTests).Directory;
           var dependabotFolder = TracerDirectory / "dependabot" / "integrations";
           var definitionsFile = BuildDirectory / FileNames.DefinitionsJson;
           var currentDependencies = DependabotFileManager.GetCurrentlyTestedVersions(dependabotFolder);
           Logger.Information("Found {CurrentDependenciesCount} existing dependencies", currentDependencies.Count);
           var excludedFromUpdates = ((IncludePackages, ExcludePackages) switch
                                         {
                                             (_, { } exclude) => currentDependencies.Where(x => ExcludePackages.Contains(x.NugetName, StringComparer.OrdinalIgnoreCase)),
                                             ({ } include, _) => currentDependencies.Where(x => !IncludePackages.Contains(x.NugetName, StringComparer.OrdinalIgnoreCase)),
                                             _ => Enumerable.Empty<(string NugetName, Version LatestTestedVersion)>()
                                         }).ToDictionary(x => x.NugetName, x => x.LatestTestedVersion, StringComparer.OrdinalIgnoreCase);

           foreach (var dep in excludedFromUpdates)
           {
               Logger.Information("Excluding package {NugetName} from update. Fixing at {Version}", dep.Key, dep.Value);
           }

           var versionGenerator = new PackageVersionGenerator(TracerDirectory, testDir, excludedFromUpdates);
           var testedVersions = await versionGenerator.GenerateVersions(Solution);

           var assemblies = MonitoringHomeDirectory
                           .GlobFiles("**/Datadog.Trace.dll")
                           .Select(x => x.ToString())
                           .ToList();

           var integrations = GenerateIntegrationDefinitions.GetAllIntegrations(assemblies, definitionsFile);
           var distinctIntegrations = await DependabotFileManager.BuildDistinctIntegrationMaps(integrations, testedVersions);

           await DependabotFileManager.UpdateIntegrations(dependabotFolder, distinctIntegrations);

           var outputPath = TracerDirectory / "build" / "supported_versions.json";
           await GenerateSupportMatrix.GenerateInstrumentationSupportMatrix(outputPath, distinctIntegrations);
       });

    Target GenerateSpanDocumentation => _ => _
        .Description("Regenerate documentation from our code models")
        .Executes(() =>
        {
            var schemaVersions = new[] { "v0", "v1" };
            foreach (var schemaVersion in schemaVersions)
            {
                var rulesFilePath = TestsDirectory / "Datadog.Trace.TestHelpers" / $"SpanMetadata{schemaVersion.ToUpper()}Rules.cs";
                var rulesOutput = RootDirectory / "docs" / "span_attribute_schema" / $"{schemaVersion}.md";

                var generator = new SpanDocumentationGenerator(rulesFilePath, rulesOutput);
                generator.Run();
            }
        });

    Target UpdateVendoredCode => _ => _
       .Description("Updates the vendored dependency code and dependabot template")
       .Executes(async () =>
       {
            var dependabotProj = TracerDirectory / "dependabot"  /  "Datadog.Dependabot.Vendors.csproj";
            DependabotFileManager.UpdateVendors(dependabotProj);

            var vendorDirectory = Solution.GetProject(Projects.DatadogTrace).Directory / "Vendors";
            var downloadDirectory = TemporaryDirectory / "Downloads";
            EnsureCleanDirectory(downloadDirectory);
            await UpdateVendorsTool.UpdateVendors(downloadDirectory, vendorDirectory);
       });

    Target UpdateVersion => _ => _
       .Description("Update the version number for the tracer")
       .Before(Clean, BuildTracerHome)
       .Before(Clean, BuildProfilerHome)
       .Requires(() => Version)
       .Requires(() => NewVersion)
       .Requires(() => NewIsPrerelease)
       .Executes(() =>
        {
            if (NewVersion == Version && IsPrerelease == NewIsPrerelease)
            {
                throw new Exception($"Cannot set versions, new version {NewVersion} was the same as {Version} and {IsPrerelease} == {NewIsPrerelease}");
            }

            // Samples need to use the latest version (i.e. the _current_ build version, before updating)
            new SetAllVersions.Samples(TracerDirectory, Version, IsPrerelease).Run();
            // Source needs to use the _actual_ version
            new SetAllVersions.Source(TracerDirectory, NewVersion, NewIsPrerelease.Value!).Run();
        });

    Target AnalyzePipelineCriticalPath => _ => _
       .Description("Perform critical path analysis on the consolidated pipeline stages")
       .Requires(() => TargetBranch)
       .Executes(async () =>
        {
            // Visit https://app.datadoghq.com/dashboard/49i-n6n-9jq and download the results you require as a csv file
            // Save the results in build_data/stages.csv, and then run this task, specifying 'master' or 'branch'.
            // e.g.
            //
            // ./tracer/build.ps1 AnalyzePipelineCriticalPath --TargetBranch master
            // ./tracer/build.ps1 AnalyzePipelineCriticalPath --TargetBranch branch
            //
            // This task
            // - Loads the list of pipelines and their dependencies
            // - Sorts the list by "dependency" order, i.e. each stage can only depend on stages earlier in the list
            // - Calculates the earliest and latest times that a dependency can run without making the project longer
            // - Visualizes the results as a mermaid diagram and writes to a markdown doc (trying to write to the console gives errors due to wrapping)
            //
            // The markdown doc can be found at `build_data/pipeline_critical_path.md`
            // Copy the contents of the diagram and paste into the text box at https://mermaid.live/ to visualize it.
            //
            // The different colours indicate the following:
            // - Stages on the critical path, which are required for merging PRs (Red box)
            // - Stages on the critical path, which are not required for merging PRs (Grey box with red outline)
            // - Stages not on the critical path, which are required for merging PRs (Blue box)
            // - Stages not on the critical path, which are not required for merging PRs (Grey box)
            var isMasterRun = TargetBranch == "master";
            await CriticalPathAnalysis.CriticalPathAnalyzer.AnalyzeCriticalPath(RootDirectory, isMasterRun);
        });

    Target UpdateSnapshots => _ => _
        .Description("Updates verified snapshots files with received ones")
        .Executes(ReplaceReceivedFilesInSnapshots);

    Target PrintSnapshotsDiff  => _ => _
      .Description("Prints snapshots differences from the current tests")
      .AssuredAfterFailure()
      .OnlyWhenStatic(() => IsServerBuild)
      .Executes(() =>
      {
          var snapshotsDirectory = TestsDirectory / "snapshots";
          var files = snapshotsDirectory.GlobFiles("*.received.*");

          foreach (var source in files)
          {
              var fileName = Path.GetFileNameWithoutExtension(source);

              Logger.Information("Difference found in " + fileName);
              var dmp = new diff_match_patch();
              var diff = dmp.diff_main(File.ReadAllText(source.ToString().Replace("received", "verified")), File.ReadAllText(source));
              dmp.diff_cleanupSemantic(diff);

              DiffHelper.PrintDiff(diff);
          }
      });

    Target UpdateSnapshotsFromBuild => _ => _
      .Description("Updates verified snapshots downloading them from the CI given a build id")
      .Requires(() => BuildId)
      .Executes(async () =>
      {
            if (!int.TryParse(BuildId, out var buildNumber))
            {
              throw new InvalidParametersException(("BuildId should be an int"));
            }

            // Connect to Azure DevOps Services
            var connection = new VssConnection(
                new Uri(AzureDevopsOrganisation),
                new VssBasicCredential(string.Empty, AzureDevopsToken));

            // Get an Azure devops client
            using var buildHttpClient = connection.GetClient<BuildHttpClient>();

            var artifacts = await buildHttpClient.GetArtifactsAsync(
                             project: AzureDevopsProjectId,
                             buildId: buildNumber);

            var listTasks = new List<Task>();
            foreach(var artifact in artifacts)
            {
                if (!artifact.Name.Contains("snapshots"))
                {
                    continue;
                }

                var extractLocation = Path.Combine((AbsolutePath)Path.GetTempPath(), artifact.Name);
                var snapshotsDirectory = TestsDirectory / "snapshots";

                listTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await DownloadAzureArtifact((AbsolutePath)Path.GetTempPath(), artifact, AzureDevopsToken);

                        CopyDirectoryRecursively(
                            source: extractLocation,
                            target: snapshotsDirectory,
                            DirectoryExistsPolicy.Merge,
                            FileExistsPolicy.Skip,
                            excludeFile: file => !Path.GetFileNameWithoutExtension(file.FullName).EndsWith(".received"));

                        DeleteDirectory(extractLocation);
                    }
                    catch (Exception e)
                    {
                        Logger.Warning(e, $"Ignoring issue downloading: '{artifact}'");
                    }
                }));
            }

            Task.WaitAll(listTasks.ToArray());

            ReplaceReceivedFilesInSnapshots();
      });

    Target RegenerateSolutions
        => _ => _
               .Description("Regenerates the 'build' solutions based on the 'master' solution")
               .Executes(() =>
                {
                    if (FastDevLoop)
                    {
                        return;
                    }
                    
                    // Create a copy of the "full solution"
                    var sln = ProjectModelTasks.CreateSolution(
                        fileName: RootDirectory / "Datadog.Trace.Samples.g.sln",
                        solutions: new[] { Solution },
                        randomizeProjectIds: false);

                    // Remove everything except the standalone test-application projects
                    sln.AllProjects
                       .Where(x => !IsTestApplication(x))
                       .ForEach(x =>
                        {
                            Logger.Information("Removing project '{Name}'", x.Name);
                            sln.RemoveProject(x);
                        });

                    sln.Save();

                    bool IsTestApplication(Project x)
                    {
                        // We explicitly don't build some of these because
                        // 1. They're a pain to build
                        // 2. They aren't actually run in the CI (something we should address in the future)
                        if (x.Name is "ExpenseItDemo" or "StackExchange.Redis.AssemblyConflict.LegacyProject" or "_build")
                        {
                            return false;
                        }

                        // Include test-applications, but exclude the following for now:
                        // - test-applications/aspnet
                        // - test-applications/security/aspnet
                        // These currently aren't published to separate folders, are minimal, can't be
                        // built on macos, and don't take long to build, so not a big value in building
                        // them separately currently
                        var solutionFolder = x.SolutionFolder;
                        while (solutionFolder is not null)
                        {
                            if(solutionFolder.Name == "aspnet"
                                && solutionFolder.SolutionFolder?.Name == "test-applications")
                            {
                                return false;
                            }

                            if(solutionFolder.Name == "aspnet"
                                && solutionFolder.SolutionFolder?.Name == "security"
                                && solutionFolder.SolutionFolder?.SolutionFolder?.Name == "test-applications")
                            {
                                return false;
                            }

                            if (solutionFolder.Name == "test-applications")
                            {
                                // Exclude projects which directly reference Datadog.Trace - these need to be
                                // built with the "main" solution as they're inherently not standalone
                                return !x.ReferencesDatadogTrace();
                            }

                            solutionFolder = solutionFolder.SolutionFolder;
                        }

                        return false;
                    }
                });

       Target DownloadBundleNugetFromBuild => _ => _
        .Description("Downloads Datadog.Trace.Bundle package from Azure DevOps and extracts it to the local bundle home directory." +
                     " Useful for building Datadog.Trace.Bundle or Datadog.AzureFunctions nupkg packages locally.")
        .Requires(() => BuildId)
        .Executes(async () =>
        {
            if (!int.TryParse(BuildId, out var buildNumber))
            {
                throw new InvalidParametersException("BuildId should be an int");
            }

            const string artifactName = "bundle-nuget-package";

            var tempRoot = TemporaryDirectory / "bundle-nuget";
            var downloadDirectory = tempRoot / "download";
            var packageExtractionDirectory = tempRoot / "package";

            EnsureCleanDirectory(tempRoot);
            EnsureExistingDirectory(downloadDirectory);
            EnsureExistingDirectory(packageExtractionDirectory);

            using var connection = new VssConnection(
                new Uri(AzureDevopsOrganisation),
                new VssBasicCredential(string.Empty, AzureDevopsToken));

            using var client = connection.GetClient<BuildHttpClient>();

            var artifact = await client.GetArtifactAsync(
                project: AzureDevopsProjectId,
                buildId: buildNumber,
                artifactName: artifactName);

            await DownloadAzureArtifact(downloadDirectory, artifact, AzureDevopsToken);

            var artifactDirectory = downloadDirectory / artifact.Name;

            var packageFile = artifactDirectory.GlobFiles("Datadog.Trace.Bundle.*.nupkg").FirstOrDefault();

            if (packageFile is null)
            {
                throw new Exception($"Datadog.Trace.Bundle package was not found in artifact '{artifact.Name}'.");
            }

            EnsureCleanDirectory(packageExtractionDirectory);
            UncompressZipQuiet(packageFile, packageExtractionDirectory);

            var contentDirectory = packageExtractionDirectory / "contentFiles" / "any" / "any" / "datadog";

            if (!contentDirectory.Exists())
            {
                throw new Exception($"Could not locate datadog content folder in extracted package at '{packageExtractionDirectory}'.");
            }

            EnsureCleanDirectory(BundleHomeDirectory);
            CopyDirectoryRecursively(contentDirectory, BundleHomeDirectory, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
        });

    private void ReplaceReceivedFilesInSnapshots()
    {
        var snapshotsDirectory = TestsDirectory / "snapshots";
        var files = snapshotsDirectory.GlobFiles("*.received.*");

        var suffixLength = "received".Length;
        foreach (var source in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(source);
            if (!fileName.EndsWith("received"))
            {
                Logger.Warning($"Skipping file '{source}' as filename did not end with 'received'");
                continue;
            }

            var trimmedName = fileName.Substring(0, fileName.Length - suffixLength);
            var dest = Path.Combine(snapshotsDirectory, $"{trimmedName}verified{Path.GetExtension(source)}");
            MoveFile(source, dest, FileExistsPolicy.Overwrite, createDirectories: true);
        }
    }

    private bool IsDebugRun()
    {
        var forceDebugRun = Environment.GetEnvironmentVariable("ForceDebugRun");
        if (!string.IsNullOrEmpty(forceDebugRun)
         && (forceDebugRun == "1" || (bool.TryParse(forceDebugRun, out var force) && force)))
        {
            return true;
        }

        var scheduleName = Environment.GetEnvironmentVariable("BUILD_CRONSCHEDULE_DISPLAYNAME");
        if (string.IsNullOrEmpty(scheduleName))
        {
            // not in CI
            return false;
        }

        return scheduleName == "Daily Debug Run";
    }

    private static MSBuildTargetPlatform GetDefaultTargetPlatform()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return ARM64TargetPlatform;
        }

        if (RuntimeInformation.OSArchitecture == Architecture.X86)
        {
            return MSBuildTargetPlatform.x86;
        }

        return MSBuildTargetPlatform.x64;
    }

    private static string GetDefaultRuntimeIdentifier(bool isAlpine)
    {
        // https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
        return (Platform, (string)GetDefaultTargetPlatform()) switch
        {
            (PlatformFamily.Windows, "x86") => "win-x86",
            (PlatformFamily.Windows, "x64") => "win-x64",

            (PlatformFamily.Linux, "x64") => isAlpine ? "linux-musl-x64" : "linux-x64",
            (PlatformFamily.Linux, "ARM64" or "ARM64EC") => isAlpine ? "linux-musl-arm64" : "linux-arm64",

            (PlatformFamily.OSX, "ARM64" or "ARM64EC") => "osx-arm64",
            _ => null
        };
    }

    private static MSBuildTargetPlatform ARM64TargetPlatform = (MSBuildTargetPlatform)"ARM64";
    private static MSBuildTargetPlatform ARM64ECTargetPlatform = (MSBuildTargetPlatform)"ARM64EC";

    /// <summary>
    /// Tries to download a file from the provided url, with a retry, and saves it at a temp path
    /// </summary>
    /// <param name="url">The URL to download from</param>
    /// <returns>The temporary path where the file has been saved</returns>
    /// <exception cref="Exception"></exception>
    private static async Task<string> DownloadFile(string url)
    {
        using var client = new HttpClient();
        var attemptsRemaining = 3;
        var defaultDelay = TimeSpan.FromSeconds(2);

        while (attemptsRemaining > 0)
        {
            var retryDelay = defaultDelay;
            try
            {
                Logger.Information("Downloading from {Url}", url);
                using var response = await client.GetAsync(url);
                var outputPath = Path.GetTempFileName();

                if (response.IsSuccessStatusCode)
                {
                    Logger.Information("Saving file to {Path}", outputPath);
                    await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    await response.Content.CopyToAsync(fs);
                    return outputPath;
                }

                Logger.Warning("Failed to download file from {Url}, {StatusCode}: {Body}", url, response.StatusCode, await response.Content.ReadAsStringAsync());

                if (response.StatusCode == HttpStatusCode.TooManyRequests
                    && response.Headers.TryGetValues("Retry-After", out var values)
                    && values.FirstOrDefault() is {} retryAfter)
                    {
                        if (int.TryParse(retryAfter, out var seconds))
                        {
                            retryDelay = TimeSpan.FromSeconds(seconds);
                        }
                        else if (DateTimeOffset.TryParse(retryAfter, out var retryDate))
                        {
                            var delta = retryDate - DateTimeOffset.UtcNow;
                            retryDelay = delta > TimeSpan.Zero ? delta : retryDelay;
                        }
                    }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error downloading file from {Url}", url);
            }

            attemptsRemaining--;
            if (attemptsRemaining > 0)
            {
                Logger.Debug("Waiting {RetryDelayTotalSeconds} seconds before retry...", retryDelay.TotalSeconds);
                await Task.Delay(retryDelay);
            }
        }

        throw new Exception("Failed to download telemetry forwarder");
    }

    static string GetSha256Hash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);

        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    static string GetSha512Hash(string filePath)
    {
        using var sha512 = SHA512.Create();
        using var stream = File.OpenRead(filePath);

        var hashBytes = sha512.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}
