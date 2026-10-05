using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Logger = Serilog.Log;

public static class ProjectExtensions
{
    // Evaluating a project's MSBuild properties in-process (Nuke's built-in Solution/Project support, via
    // Microsoft.Build.Locator) loads the active .NET SDK's own MSBuild assemblies - including its bundled
    // NuGet.Frameworks - into this process. That's usually fine, but evaluating a Microsoft.NET.Sdk.Web
    // project (e.g. Datadog.Trace.IntegrationTests) also needs MSBuild's NuGet.Frameworks to evaluate
    // "[MSBuild]::GetTargetPlatformIdentifier(...)" in Sdk.props - and if a *different* exact version of
    // NuGet.Frameworks is already loaded in-process (e.g. because Nuke.Common's Host.Activation force-loads
    // types from every already-loaded assembly during startup), CoreCLR can't bind two versions of the same
    // strong-named assembly in one AssemblyLoadContext, and MSBuild throws.
    //
    // To avoid this class of conflict entirely, evaluate projects out-of-process via `dotnet msbuild
    // -getProperty`, which runs MSBuild in its own process and only prints the requested property values as
    // JSON - no MSBuild (or NuGet.Frameworks) assembly is ever loaded into this process.
    static readonly string[] PropertyNames = { "TargetFramework", "TargetFrameworks", "RequiresDockerDependency", "AllowDatadogTraceReference" };

    static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> EvaluatedProperties = new();

    public static IReadOnlyCollection<string> TryGetTargetFrameworks(this Project project)
    {
        try
        {
            var properties = GetEvaluatedProperties(project);
            var value = properties["TargetFramework"] is { Length: > 0 } tfm ? tfm : properties["TargetFrameworks"];
            return string.IsNullOrEmpty(value) ? null : value.Split(';');
        }
        catch (Exception ex)
        {
            Logger.Information($"Error fetching target frameworks for {project?.Name}: {ex}");
            return null;
        }
    }

    public static DockerDependencyType RequiresDockerDependency(this Project project)
    {
        try
        {
            var value = GetEvaluatedProperties(project)["RequiresDockerDependency"];
            return string.IsNullOrEmpty(value) ? DockerDependencyType.None : Enum.Parse<DockerDependencyType>(value);
        }
        catch (Exception ex)
        {
            Logger.Information($"Error checking RequiresDockerDependency for {project?.Name}: {ex}");
            return DockerDependencyType.None;
        }
    }

    public static bool ReferencesDatadogTrace(this Project project)
    {
        try
        {
            if (Path.GetExtension(project.Path) != ".csproj")
            {
                return false;
            }

            var value = GetEvaluatedProperties(project)["AllowDatadogTraceReference"];
            return bool.TryParse(value, out var result) && result;
        }
        catch (Exception ex)
        {
            Logger.Information(ex, "Error checking ReferencesDatadogTrace for {ProjectName}", project?.Name);
            return false;
        }
    }

    // Cached per project path, so that the three properties above are fetched via a single out-of-process
    // MSBuild evaluation per project (evaluation is expensive), regardless of how many of them are queried.
    static IReadOnlyDictionary<string, string> GetEvaluatedProperties(Project project) =>
        EvaluatedProperties.GetOrAdd(project.Path, EvaluateProperties);

    static IReadOnlyDictionary<string, string> EvaluateProperties(string projectPath)
    {
        var arguments = $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:{string.Join(",", PropertyNames)}";
        var process = ProcessTasks.StartProcess("dotnet", arguments, logOutput: false, logInvocation: false);
        process.AssertZeroExitCode();

        var json = string.Join(Environment.NewLine, process.Output.Select(x => x.Text));
        var result = JsonConvert.DeserializeObject<GetPropertyResult>(json);
        return result.Properties;
    }

    class GetPropertyResult
    {
        public Dictionary<string, string> Properties { get; set; }
    }
}
