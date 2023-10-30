using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
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

    [LazyLocalExecutable(@"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\gacutil.exe")]
    readonly Lazy<Tool> GacUtil;
    [LazyLocalExecutable(@"C:\Program Files\IIS Express\iisexpress.exe")]
    readonly Lazy<Tool> IisExpress;

    AbsolutePath IisExpressApplicationConfig =>
        RootDirectory / ".vs" / Solution.Name / "config" / "applicationhost.config";

    readonly IEnumerable<string> GacProjects = new []
    {
        Projects.DatadogTrace,
        Projects.DatadogTraceAspNet
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
           var testDir = Solution.GetProject(Projects.ClrProfilerIntegrationTests).Directory;

           var versionGenerator = new PackageVersionGenerator(TracerDirectory, testDir);
           await versionGenerator.GenerateVersions(Solution);

           var assemblies = MonitoringHomeDirectory
                           .GlobFiles("**/Datadog.Trace.dll")
                           .Select(x => x.ToString())
                           .ToList();

           var integrations = GenerateIntegrationDefinitions.GetAllIntegrations(assemblies);

           var dependabotProj = TracerDirectory / "dependabot" / "Datadog.Dependabot.Integrations.csproj";
           await DependabotFileManager.UpdateIntegrations(dependabotProj, integrations);
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
            if (NewVersion == Version)
            {
                throw new Exception($"Cannot set versions, new version {NewVersion} was the same as {Version}");
            }

            // Samples need to use the latest version (i.e. the _current_ build version, before updating)
            new SetAllVersions.Samples(TracerDirectory, Version, IsPrerelease).Run();
            // Source needs to use the _actual_ version
            new SetAllVersions.Source(TracerDirectory, NewVersion, NewIsPrerelease.Value!).Run();
        });

    Target AnalyzePipelineCriticalPath => _ => _
       .Description("Perform critical path analysis on the consolidated pipeline stages")
       .Executes(async () =>
        {
            await CriticalPathAnalysis.CriticalPathAnalyzer.AnalyzeCriticalPath(RootDirectory);
        });

    Target UpdateSnapshots => _ => _
        .Description("Updates verified snapshots files with received ones")
        .Executes(ReplaceReceivedFilesInSnapshots);

    Target PrintSnapshotsDiff  => _ => _
      .Description("Prints snapshots differences from the current tests")
      .AssuredAfterFailure()
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

              foreach (var t in diff)
              {
                  if (t.operation != Operation.EQUAL)
                  {
                      Logger.Information(DiffToString(t));
                  }
              }
          }

          string DiffToString(Diff diff)
          {
              var line = diff.operation switch
              {
                  Operation.DELETE => $"- {diff.text}",
                  Operation.INSERT => $"+ {diff.text}",
                  Operation.EQUAL => string.Empty,
                  _ => throw new Exception("Unknown value of the Option enum.")
              };
              return line.Trim('\n');
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
                    await DownloadAzureArtifact((AbsolutePath)Path.GetTempPath(), artifact, AzureDevopsToken);

                    CopyDirectoryRecursively(
                        source: extractLocation,
                        target: snapshotsDirectory,
                        DirectoryExistsPolicy.Merge,
                        FileExistsPolicy.Skip,
                        excludeFile: file => !Path.GetFileNameWithoutExtension(file.FullName).EndsWith(".received"));

                    DeleteDirectory(extractLocation);
                }));
            }

            Task.WaitAll(listTasks.ToArray());

            ReplaceReceivedFilesInSnapshots();
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

            if (fileName.Contains("VersionMismatchNewerNugetTests"))
            {
                Logger.Warning("Updated snapshots contain a version mismatch test. You may need to upgrade your code in the Azure public feed.");
            }

            var trimmedName = fileName.Substring(0, fileName.Length - suffixLength);
            var dest = Path.Combine(snapshotsDirectory, $"{trimmedName}verified{Path.GetExtension(source)}");
            MoveFile(source, dest, FileExistsPolicy.Overwrite, createDirectories: true);
        }
    }

    private async Task<bool> IsDebugRun()
    {
        var forceDebugRun = Environment.GetEnvironmentVariable("ForceDebugRun");
        if (!string.IsNullOrEmpty(forceDebugRun)
         && (forceDebugRun == "1" || (bool.TryParse(forceDebugRun, out var force) && force)))
        {
            return true;
        }

        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
        if (string.IsNullOrEmpty(buildId))
        {
            // not in CI
            return false;
        }

        try
        {
            var azDoApi = $"https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/{buildId}";
            using var client = new HttpClient();
            using var stream = await client.GetStreamAsync(azDoApi);
            using var json = await JsonDocument.ParseAsync(stream);
            var triggerInfo = json.RootElement.GetProperty("triggerInfo");
            if (triggerInfo.TryGetProperty("scheduleName", out var scheduleNameElement))
            {
                var scheduleName = scheduleNameElement.ToString();
                return scheduleName == "Daily Debug Run";
            }
            else
            {
                // scheduleName not found - this will happen on PRs etc
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error calling Azdo API to check for debug run");
            return false;
        }
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

    private static MSBuildTargetPlatform ARM64TargetPlatform = (MSBuildTargetPlatform)"ARM64";
    private static MSBuildTargetPlatform ARM64ECTargetPlatform = (MSBuildTargetPlatform)"ARM64EC";
}
