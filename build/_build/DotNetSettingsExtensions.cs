using System;
using System.Collections.Generic;
using System.IO;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;

internal static partial class DotNetSettingsExtensions
{
    public static DotNetBuildSettings SetTargetPlatformAnyCPU(this DotNetBuildSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetTestSettings SetTargetPlatformAnyCPU(this DotNetTestSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetPublishSettings SetTargetPlatformAnyCPU(this DotNetPublishSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static T SetTargetPlatformAnyCPU<T>(this T settings)
        where T: MSBuildSettings
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetBuildSettings SetTargetPlatform(this DotNetBuildSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }
    public static DotNetTestSettings SetTargetPlatform(this DotNetTestSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetPublishSettings SetTargetPlatform(this DotNetPublishSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    private static string GetTargetPlatform(MSBuildTargetPlatform platform) =>
        platform == MSBuildTargetPlatform.MSIL ? "AnyCPU" : platform.ToString();

    public static T SetNoWarnDotNetCore3<T>(this T settings)
        where T: ToolSettings
    {
        return settings.SetProcessArgumentConfigurator(
            arg => arg.Add("/nowarn:netsdk1138"));
    }
    
    public static T SetPlatform<T>(this T settings, MSBuildTargetPlatform platform)
        where T: NuGetRestoreSettings
    {
        return settings.SetProcessArgumentConfigurator(
            arg => arg.Add($"/p:\"Platform={platform}\""));
    }
    
    public static T SetDDEnvironmentVariables<T>(this T settings, string serviceName)
        where T: ToolSettings
    {
        return settings.SetProcessEnvironmentVariable("DD_SERVICE_NAME", serviceName);
    }
    
    public static T SetProcessEnvironmentVariables<T>(this T settings, IEnumerable<KeyValuePair<string, string>> variables)
        where T: ToolSettings
    {
        foreach (var keyValuePair in variables)
        {
            settings = settings.SetProcessEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);
        }

        return settings;
    }
    
    public static T EnableNoDependencies<T>(this T settings)
        where T: MSBuildSettings
    {
        return settings.SetProperty("BuildProjectReferences", false);
    }

    public static DotNetTestSettings EnableMemoryDumps(this DotNetTestSettings settings, MiniDumpType dumpType = MiniDumpType.MiniDumpWithPrivateReadWriteMemory)
    {
        dumpType = dumpType != MiniDumpType.Default ? dumpType : MiniDumpType.MiniDumpWithPrivateReadWriteMemory;
        return settings
            .SetProcessEnvironmentVariable("COMPlus_DbgEnableMiniDump", "1")
            .SetProcessEnvironmentVariable("COMPlus_DbgMiniDumpType", ((int) dumpType).ToString());
    }

    public static DotNetTestSettings EnableTrxLogOutput(this DotNetTestSettings settings, string resultsDirectory)
    {
        return settings
            .SetLogger("trx")
            .SetResultsDirectory(resultsDirectory);
    }

    /// <summary>
    /// GitLab installs MSBuild in a non-standard place that causes issues for Nuke trying to resolve it
    /// </summary>
    public static MSBuildSettings SetMSBuildPath(this MSBuildSettings settings)
    {
        var vsRoot = Environment.GetEnvironmentVariable("VSTUDIO_ROOT");

        return settings
            .When(!string.IsNullOrEmpty(vsRoot),
                c => c.SetProcessToolPath(Path.Combine(vsRoot, "MSBuild", "Current", "Bin", "MSBuild.exe")));
    }
}
