using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;

[PublicAPI]
[ExcludeFromCodeCoverage]
[Serializable]
public class PrefixedDotNetTestSettings : DotNetTestSettings
{
    public string PrefixTool { get; set; }

    public override string ProcessToolPath =>
        string.IsNullOrWhiteSpace(PrefixTool) ? base.ProcessToolPath : $"{PrefixTool} {base.ProcessToolPath}";
}

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class PrefixedDotNetTestTask
{
    public static Action<OutputType, string> DotNetLogger { get; set; } = CustomLogger;

    public static IReadOnlyCollection<Output> PrefixedDotNetTest(PrefixedDotNetTestSettings toolSettings = null)
    {
        toolSettings = toolSettings ?? new PrefixedDotNetTestSettings();
        using var process = ProcessTasks.StartProcess(toolSettings);
        process.AssertZeroExitCode();
        return process.Output;
    }

    public static IEnumerable<(PrefixedDotNetTestSettings Settings, IReadOnlyCollection<Output> Output)> PrefixedDotNetTest(CombinatorialConfigure<PrefixedDotNetTestSettings> configurator, int degreeOfParallelism = 1, bool completeOnFailure = false)
    {
        return configurator.Invoke(PrefixedDotNetTest, DotNetLogger, degreeOfParallelism, completeOnFailure);
    }

    public static PrefixedDotNetTestSettings ToPrefixed(this DotNetTestSettings from) => (PrefixedDotNetTestSettings)from;

    public static Configure<PrefixedDotNetTestSettings> ToPrefixed(Configure<DotNetTestSettings> from)
    {
        return arg => (PrefixedDotNetTestSettings)from(arg);
    }

    public static PrefixedDotNetTestSettings SetPrefixTool(this PrefixedDotNetTestSettings config, string prefixTool)
    {
        config.PrefixTool = prefixTool;
        return config;
    }

    public static PrefixedDotNetTestSettings SetTargetPlatform(this PrefixedDotNetTestSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    private static string GetTargetPlatform(MSBuildTargetPlatform platform) =>
        platform == MSBuildTargetPlatform.MSIL ? "AnyCPU" : platform.ToString();

    internal static void CustomLogger(OutputType type, string output)
    {
        if (type == OutputType.Err)
        {
            Logger.Error(output);
            return;
        }

        var spaces = 0;
        for (var i = 0; i < output.Length && spaces < 3; i++)
        {
            if (output[i] == ' ')
            {
                spaces++;
                continue;
            }

            if (i >= 4 &&
                'e' == output[i - 4] &&
                'r' == output[i - 3] &&
                'r' == output[i - 2] &&
                'o' == output[i - 1] &&
                'r' == output[i])
            {
                Logger.Error(output);
                return;
            }

            if (i >= 6 &&
                'w' == output[i - 6] &&
                'a' == output[i - 5] &&
                'r' == output[i - 4] &&
                'n' == output[i - 3] &&
                'i' == output[i - 2] &&
                'n' == output[i - 1] &&
                'g' == output[i])
            {
                Logger.Warn(output);
                return;
            }
        }

        Logger.Normal(output);
    }
}
