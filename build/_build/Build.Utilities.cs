using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;
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
    
    [Parameter("The TargetFramework to execute when running or building a sample app")] 
    readonly string SampleFramework;
    
    [Parameter("Additional environment variables, in the format KEY1=Value1 Key2=Value2 to use when running the IIS Sample")] 
    readonly string[] ExtraEnvVars;
    
    [LazyLocalExecutable(@"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\gacutil.exe")] 
    readonly Lazy<Tool> GacUtil;
    [LazyLocalExecutable(@"C:\Program Files\IIS Express\iisexpress.exe")] 
    readonly Lazy<Tool> IisExpress;
    [LazyLocalExecutable(@"C:\Program Files\IIS Express\appcmd.exe")] 
    readonly Lazy<Tool> AppCmd;

    AbsolutePath IisExpressApplicationConfig =>
        RootDirectory / ".vs" / Solution.Name / "config" / "applicationhost.config";
    
    readonly IEnumerable<string> GacProjects = new []
    {
        Projects.DatadogTrace,
        Projects.DatadogTraceAspNet,
        Projects.ClrProfilerManaged,
        Projects.ClrProfilerManagedCore,
    };

    Target GacAdd => _ => _
        .Description("Adds the (already built) files to the Windows GAC **REQUIRES ELEVATED PERMISSIONS** ")
        .Requires(() => IsWin)
        .DependsOn(GacRemove)
        .After(BuildTracerHome)
        .Executes(() =>
        {
            foreach (var dll in GacProjects)
            {
                var path = TracerHomeDirectory / "net461" / $"{dll}.dll";
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

    Target InstallTracingModuleInIis => _ => _
        .Description("Installs the tracing module in the local applicationhost.config for IIS Express")
        .Requires(() => IsWin)
        .Executes(() =>
        {
            AppCmd.Value(
                $"set config /apphostconfig:\"{IisExpressApplicationConfig}\" /section:system.webServer/modules /+[name='DatadogTracingModule',type='Datadog.Trace.AspNet.TracingHttpModule,Datadog.Trace.AspNet,Version=1.0.0.0,Culture=neutral,PublicKeyToken=def86d061d0d2eeb',preCondition='managedHandler,runtimeVersionv4.0']");
        });

    Target BuildIisSampleApp => _ => _
        .Description("Rebuilds an IIS sample app")
        .Requires(() => SampleName)
        .Requires(() => Solution.GetProject(SampleName) != null)
        .Executes(() =>
        {
            MSBuild(s => s
                .SetConfiguration(Configuration)
                .SetTargetPlatform(Platform)
                .SetProjectFile(Solution.GetProject(SampleName)));
        });
    
    Target RunIisSample => _ => _
        .Description("Runs an IIS sample app, enabling profiling.")
        .Requires(() => SampleName)
        .Requires(() => IsWin)
        .Executes(() =>
        {
            var envVars = new Dictionary<string,string>(new ProcessStartInfo().Environment)
            {
                {"COR_ENABLE_PROFILING", "1"},
                {"COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
                {"COR_PROFILER_PATH", TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll" },
                {"DD_INTEGRATIONS", TracerHomeDirectory / "integrations.json" },
                {"DD_DOTNET_TRACER_HOME", TracerHomeDirectory },
            };
            
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
        .Requires(() => SampleFramework)
        .Requires(() => IsWin)
        .Executes(() =>
        {
            var envVars = new Dictionary<string,string>()
            {
                {"COR_ENABLE_PROFILING", "1"},
                {"COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
                {"COR_PROFILER_PATH", TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll" },
                {"CORECLR_ENABLE_PROFILING", "1"},
                {"CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"},
                {"CORECLR_PROFILER_PATH", TracerHomeDirectory / "win-x64" / "Datadog.Trace.ClrProfiler.Native.dll" },
                {"DD_INTEGRATIONS", TracerHomeDirectory / "integrations.json" },
                {"DD_DOTNET_TRACER_HOME", TracerHomeDirectory },
                {"ASPNETCORE_URLS", "https://*:5003" },
            };
            
            if (ExtraEnvVars?.Length > 0)
            {
                foreach (var envVar in ExtraEnvVars)
                {
                    var kvp = envVar.Split('=');
                    envVars[kvp[0]] = kvp[1];
                }
            }
            
            Logger.Info($"Running sample '{SampleName}'");

            var project = Solution.GetProject(SampleName)?.Path;
            if (project is null)
            {
                throw new Exception($"Could not find project in solution with name '{SampleName}'");
            }
            
            DotNetBuild(s => s
                .SetFramework(SampleFramework)
                .SetProjectFile(project)
                .SetConfiguration(Configuration)
                .SetNoWarnDotNetCore3()
                .SetProperty("platform", Platform));
            
            DotNetRun(s => s
                .SetFramework(SampleFramework)
                .EnableNoLaunchProfile()
                .SetProjectFile(project)
                .SetConfiguration(Configuration)
                .SetNoWarnDotNetCore3()
                .SetProperty("platform", Platform)
                .SetProcessEnvironmentVariables(envVars));

        });
}
