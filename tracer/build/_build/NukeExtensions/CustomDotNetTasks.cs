using System.Collections.Generic;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

public static class CustomDotNetTasks
{
    /// <summary>
    ///   <p>The <c>dotnet msbuild</c> command allows access to a fully functional MSBuild.</p><p>The command has the exact same capabilities as the existing MSBuild command-line client for SDK-style projects only. The options are all the same. For more information about the available options, see the [MSBuild command-line reference](/visualstudio/msbuild/msbuild-command-line-reference).</p><p>The [dotnet build](dotnet-build.md) command is equivalent to <c>dotnet msbuild -restore</c>. When you don't want to build the project and you have a specific target you want to run, use <c>dotnet build</c> or <c>>dotnet msbuild</c> and specify the target.</p>
    ///   <p>For more details, visit the <a href="https://docs.microsoft.com/en-us/dotnet/core/tools/">official website</a>.</p>
    /// </summary>
    /// <remarks>
    ///   <p>This is a <a href="http://www.nuke.build/docs/authoring-builds/cli-tools.html#fluent-apis">CLI wrapper with fluent API</a> that allows to modify the following arguments:</p>
    /// </remarks>
    public static IReadOnlyCollection<Output> DotNetMSBuild(DotNetMSBuildSettings toolSettings = null)
    {
        toolSettings = toolSettings ?? new DotNetMSBuildSettings();
        using var process = ProcessTasks.StartProcess(toolSettings);
        process.AssertZeroExitCode();
        return process.Output;
    }
    /// <summary>
    ///   <p>The <c>dotnet msbuild</c> command allows access to a fully functional MSBuild.</p><p>The command has the exact same capabilities as the existing MSBuild command-line client for SDK-style projects only. The options are all the same. For more information about the available options, see the [MSBuild command-line reference](/visualstudio/msbuild/msbuild-command-line-reference).</p><p>The [dotnet build](dotnet-build.md) command is equivalent to <c>dotnet msbuild -restore</c>. When you don't want to build the project and you have a specific target you want to run, use <c>dotnet build</c> or <c>>dotnet msbuild</c> and specify the target.</p>
    ///   <p>For more details, visit the <a href="https://docs.microsoft.com/en-us/dotnet/core/tools/">official website</a>.</p>
    /// </summary>
    /// <remarks>
    ///   <p>This is a <a href="http://www.nuke.build/docs/authoring-builds/cli-tools.html#fluent-apis">CLI wrapper with fluent API</a> that allows to modify the following arguments:</p>
    /// </remarks>
    public static IReadOnlyCollection<Output> DotNetMSBuild(Configure<DotNetMSBuildSettings> configurator)
    {
        return DotNetMSBuild(configurator(new DotNetMSBuildSettings()));
    }
    /// <summary>
    ///   <p>The <c>dotnet msbuild</c> command allows access to a fully functional MSBuild.</p><p>The command has the exact same capabilities as the existing MSBuild command-line client for SDK-style projects only. The options are all the same. For more information about the available options, see the [MSBuild command-line reference](/visualstudio/msbuild/msbuild-command-line-reference).</p><p>The [dotnet build](dotnet-build.md) command is equivalent to <c>dotnet msbuild -restore</c>. When you don't want to build the project and you have a specific target you want to run, use <c>dotnet build</c> or <c>>dotnet msbuild</c> and specify the target.</p>
    ///   <p>For more details, visit the <a href="https://docs.microsoft.com/en-us/dotnet/core/tools/">official website</a>.</p>
    /// </summary>
    /// <remarks>
    ///   <p>This is a <a href="http://www.nuke.build/docs/authoring-builds/cli-tools.html#fluent-apis">CLI wrapper with fluent API</a> that allows to modify the following arguments:</p>
    /// </remarks>
    public static IEnumerable<(DotNetMSBuildSettings Settings, IReadOnlyCollection<Output> Output)> DotNetMSBuild(CombinatorialConfigure<DotNetMSBuildSettings> configurator, int degreeOfParallelism = 1, bool completeOnFailure = false)
    {
        return configurator.Invoke(DotNetMSBuild, DotNetTasks.DotNetLogger, degreeOfParallelism, completeOnFailure);
    }
}
