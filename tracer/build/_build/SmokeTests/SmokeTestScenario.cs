#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public abstract record SmokeTestScenario
{
    public required string ShortName { get; init; }
    public required string PublishFramework { get; init; }
    public required string RuntimeTag { get; init; }
    public required string DockerImageRepo { get; init; }
    public required string Os { get; init; }
    public required string OsVersion { get; init; }

    public bool RunCrashTest { get; init; } = true;
    public bool ExcludeWhenPrerelease { get; init; }
    public bool IsNoop { get; init; }
    public string? SnapshotFile { get; init; }
    public string? ExtraSnapshotIgnoredAttrs { get; init; }

    public string JobName => $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";
    public string RuntimeImage => $"{DockerImageRepo}:{RuntimeTag}";
    public bool IsWindows => Os == "windows";
}

public record InstallerScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record ChiseledScenario : SmokeTestScenario
{
    public bool IsArm64 { get; init; }
}

public record NuGetScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
    public string RelativeProfilerPath => $"datadog/{RuntimeId}/Datadog.Trace.ClrProfiler.Native.so";
    public string RelativeApiWrapperPath => $"datadog/{RuntimeId}/Datadog.Linux.ApiWrapper.x64.so";
}

public record DotnetToolScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
}

public record DotnetToolNugetScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
}

public record SelfInstrumentScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record TrimmingScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public required string RuntimeId { get; init; }
    public required string PackageName { get; init; }
    public string? PackageVersionSuffix { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record WindowsMsiScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
}

public record WindowsNuGetScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
    public required string RelativeProfilerPath { get; init; }
}

public record WindowsDotnetToolScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
}

public record WindowsTracerHomeScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
    public required string RelativeProfilerPath { get; init; }
}

public record WindowsFleetInstallerIisScenario : SmokeTestScenario
{
    public required string TargetPlatform { get; init; }
    public required string FleetInstallerCommand { get; init; }
}
