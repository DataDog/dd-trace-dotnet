using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities;

[PublicAPI]
[ExcludeFromCodeCoverage]
[Serializable]
public class DotNetTestWithDumpSettings : DotNetTestSettings
{
    protected override Arguments ConfigureProcessArguments(Arguments arguments)
    {
        var toolPath = ProcessToolPath;
        if (!Path.IsPathRooted(toolPath) && !toolPath.Contains(Path.DirectorySeparatorChar))
            toolPath = ToolPathResolver.GetPathExecutable(toolPath);

        var toolPathOverride = GetToolPathOverride(toolPath);
        if (!string.IsNullOrEmpty(toolPathOverride))
        {
            toolPath = toolPathOverride;
        }

        arguments.Add("dumponexception");
        arguments.Add("-p {value}", 50);
        arguments.Add("-f none --");
        arguments.Add($"\"{toolPath}\"");
        arguments = base.ConfigureProcessArguments(arguments);

        Nuke.Common.Logger.Info($"ProcessToolPath: {toolPath}");
        Nuke.Common.Logger.Info($"RenderForExecution: {arguments.RenderForExecution()}");
        Nuke.Common.Logger.Info($"RenderForOutput: {arguments.RenderForOutput()}");

        return arguments;
    }

    [CanBeNull]
    private static string GetToolPathOverride(string toolPath)
    {
        if (toolPath.EndsWithOrdinalIgnoreCase(".dll"))
        {
            return ToolPathResolver.TryGetEnvironmentExecutable("DOTNET_EXE") ??
                   ToolPathResolver.GetPathExecutable("dotnet");
        }

#if NETCORE
            if (EnvironmentInfo.IsUnix &&
                toolPath.EndsWithOrdinalIgnoreCase(".exe") &&
                !EnvironmentInfo.IsWsl)
                return ToolPathResolver.GetPathExecutable("mono");
#endif

        return null;
    }
}

public static class DotNetTestWithDumpTask
{
    public static IProcess StartProcess(ToolSettings toolSettings)
    {
        var arguments = toolSettings.GetProcessArguments();
        var argumentsRenderForExecution = arguments.RenderForExecution();
        argumentsRenderForExecution = argumentsRenderForExecution.Replace("\"", "\\\"");

        return ProcessTasks.StartProcess(
            toolSettings.ProcessToolPath,
            argumentsRenderForExecution,
            toolSettings.ProcessWorkingDirectory,
            toolSettings.ProcessEnvironmentVariables,
            toolSettings.ProcessExecutionTimeout,
            toolSettings.ProcessLogOutput,
            toolSettings.ProcessLogInvocation,
            toolSettings.ProcessLogTimestamp,
            toolSettings.ProcessLogFile,
            toolSettings.ProcessCustomLogger,
            arguments.FilterSecrets);
    }

    public static IEnumerable<(DotNetTestWithDumpSettings Settings, IReadOnlyCollection<Output> Output)> DotNetRunWithDump(CombinatorialConfigure<DotNetTestWithDumpSettings> configurator, int degreeOfParallelism = 1, bool completeOnFailure = false)
    {
        return configurator.Invoke(DotNetRunWithDump, DotNetTasks.DotNetLogger, degreeOfParallelism, completeOnFailure);
    }

    public static IReadOnlyCollection<Output> DotNetRunWithDump(DotNetTestWithDumpSettings toolSettings = null)
    {
        toolSettings = toolSettings ?? new DotNetTestWithDumpSettings();
        using var process = StartProcess(toolSettings);
        process.AssertZeroExitCode();
        return process.Output;
    }

    public static DotNetTestWithDumpSettings WithDump(this DotNetTestSettings from) => (DotNetTestWithDumpSettings)from;

    public static Configure<DotNetTestWithDumpSettings> WithDump(Configure<DotNetTestSettings> from)
    {
        return arg => (DotNetTestWithDumpSettings)from(arg);
    }

    public static DotNetTestWithDumpSettings SetTargetPlatform(this DotNetTestWithDumpSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    private static string GetTargetPlatform(MSBuildTargetPlatform platform) =>
        platform == MSBuildTargetPlatform.MSIL ? "AnyCPU" : platform.ToString();
}
