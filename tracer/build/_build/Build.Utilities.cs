using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using GeneratePackageVersions;
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

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Parameter("The sample name to execute when running or building sample apps")]
    readonly string SampleName;

    [Parameter("Additional environment variables, in the format KEY1=Value1 Key2=Value2 to use when running the IIS Sample")]
    readonly string[] ExtraEnvVars;

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
                var path = TracerHomeDirectory / Framework / $"{dll}.dll";
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

            // Override environment variables
            envVars["COR_ENABLE_PROFILING"] = "1";
            envVars["COR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
            envVars["COR_PROFILER_PATH_64"] = TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll";
            envVars["COR_PROFILER_PATH_32"] = TracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll";
            envVars["DD_INTEGRATIONS"] = TracerHomeDirectory / "integrations.json";
            envVars["DD_DOTNET_TRACER_HOME"] = TracerHomeDirectory;

            if (ExtraEnvVars?.Length > 0)
            {
                foreach (var envVar in ExtraEnvVars)
                {
                    var kvp = envVar.Split('=');
                    envVars[kvp[0]] = kvp[1];
                }
            }

            Logger.Info($"Running sample '{SampleName}' in IIS Express");
            IisExpress.Value(
                arguments: $"/config:\"{IisExpressApplicationConfig}\" /site:{SampleName} /appPool:Clr4IntegratedAppPool",
                environmentVariables: envVars);
        });

    Target RunDotNetSample => _ => _
        .Description("Builds and runs a sample app using dotnet run, enabling profiling.")
        .Requires(() => SampleName)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var envVars = new Dictionary<string,string>()
            {
                {"COR_ENABLE_PROFILING", "1"},
                {"COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
                {"COR_PROFILER_PATH_32", TracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll"},
                {"COR_PROFILER_PATH_64", TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll"},
                {"CORECLR_ENABLE_PROFILING", "1"},
                {"CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
                {"DD_INTEGRATIONS", TracerHomeDirectory / "integrations.json" },
                {"DD_DOTNET_TRACER_HOME", TracerHomeDirectory },
                {"ASPNETCORE_URLS", "http://*:5003" },
            };

            if (IsWin)
            {
                envVars.Add("CORECLR_PROFILER_PATH_32", TracerHomeDirectory / "win-x86" / "Datadog.Trace.ClrProfiler.Native.dll");
                envVars.Add("CORECLR_PROFILER_PATH_64", TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll");
            }
            else
            {
                envVars.Add("CORECLR_PROFILER_PATH", TracerHomeDirectory / "Datadog.Trace.ClrProfiler.Native.so");
            }

            if (ExtraEnvVars?.Length > 0)
            {
                foreach (var envVar in ExtraEnvVars)
                {
                    var kvp = envVar.Split('=');
                    envVars[kvp[0]] = kvp[1];
                }
            }

            string project = Solution.GetProject(SampleName)?.Path;
            if (project is not null)
            {
                Logger.Info($"Running sample '{SampleName}'");
            }
            else if(System.IO.File.Exists(SampleName))
            {
                project = SampleName;
                Logger.Info($"Running project '{SampleName}'");
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
       .Executes(async () =>
       {
           var testDir = Solution.GetProject(Projects.ClrProfilerIntegrationTests).Directory;

           var versionGenerator = new PackageVersionGenerator(TracerDirectory, testDir);
           await versionGenerator.GenerateVersions();
       });

    Target UpdateVendoredCode => _ => _
       .Description("Updates the vendored dependency code and dependabot template")
       .Executes(() =>
       {
            var honeypotProject = RootDirectory / "honeypot"  /  "Datadog.Dependabot.Honeypot.csproj";
            UpdateVendorsTool.UpdateHoneypotProject(honeypotProject);

            var vendorDirectory = Solution.GetProject(Projects.DatadogTrace).Directory / "Vendors";
            var downloadDirectory = TemporaryDirectory / "Downloads";
            EnsureCleanDirectory(downloadDirectory);
            UpdateVendorsTool.UpdateVendors(downloadDirectory, vendorDirectory);
       });

    Target UpdateIntegrationsJson => _ => _
       .Description("Update the integrations.json file")
       .DependsOn(Clean, Restore, CreateRequiredDirectories, CompileManagedSrc, PublishManagedProfiler) // We load the dlls from the output, so need to do a clean build
       .Before(CopyIntegrationsJson)
       .Executes(() =>
        {
            var assemblies = TracerHomeDirectory
                            .GlobFiles("**/Datadog.Trace.dll")
                            .Select(x => x.ToString())
                            .ToList();

            GenerateIntegrationDefinitions.Run(assemblies, TracerDirectory);
        });

    Target UpdateVersion => _ => _
       .Description("Update the version number for the tracer")
       .Before(Clean, BuildTracerHome)
       .Requires(() => Version)
       .Executes(() =>
        {
            new SetAllVersions(TracerDirectory, Version, IsPrerelease).Run();
        });

    Target UpdateMsiContents => _ => _
       .Description("Update the contents of the MSI")
       .DependsOn(Clean, BuildTracerHome)
       .Executes(() =>
        {
            SyncMsiContent.Run(TracerDirectory, TracerHomeDirectory);
        });

    Target CiAppFeatureTracking => _ => _
       .Description("Generate the CIApp FeatureTracking JSON")
       .DependsOn(Clean, Restore, CreateRequiredDirectories, CompileManagedSrc, PublishManagedProfiler) // We load the dlls from the output, so need to do a clean build
       .Executes(() =>
        {
            // Just grab the first one for now
            var assemblyPath = TracerHomeDirectory
                            .GlobFiles("**/netcoreapp*/Datadog.Trace.dll")
                            .Select(x => x.ToString())
                            .First();

            var assembly = Assembly.LoadFrom(assemblyPath);
            var featureTrackingAttribute = assembly.GetType("Datadog.Trace.Ci.FeatureTrackingAttribute");
            var types = new[] { "Datadog.Trace.Ci.CommonTags", "Datadog.Trace.Ci.TestTags" }
                       .Select(type => assembly.GetType(type))
                       .ToArray();

            var values = GetFeatureTrackingValueFromType(types);

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(values);
            var output = OutputDirectory / "ci-app-spec.json";

            Logger.Info($"Writing spec to '{output}'");
            File.WriteAllText(output, json);

            IEnumerable<string> GetFeatureTrackingValueFromType(params Type[] types)
            {
                return types
                      .SelectMany(type => type.GetFields())
                      .Where(f => f.GetCustomAttributes(featureTrackingAttribute, true).Length > 0)
                      .Select(f => f.GetValue(null).ToString())
                      .OrderBy(v => v);
            }
        });

}
