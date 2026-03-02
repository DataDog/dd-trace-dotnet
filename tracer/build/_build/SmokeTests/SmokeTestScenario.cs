#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public abstract record SmokeTestScenario
{
    // Category: set via init in derived types that need multiple categories,
    // or overridden with a fixed value in types with a single category
    public virtual SmokeTestCategory Category { get; init; }

    // Required common properties
    public required string ShortName { get; init; }
    public required string PublishFramework { get; init; }
    public required string RuntimeTag { get; init; }
    public required string DockerImageRepo { get; init; }
    public required string Os { get; init; }
    public required string OsVersion { get; init; }

    // Optional common properties with defaults
    public bool RunCrashTest { get; init; } = true;
    public bool ExcludeWhenPrerelease { get; init; }
    public bool IsNoop { get; init; }
    public string? SnapshotFile { get; init; }
    public string? ExtraSnapshotIgnoredAttrs { get; init; }

    // Computed properties
    public string JobName => $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string FullName => $"{Category}_{JobName}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";
    public string RuntimeImage => $"{DockerImageRepo}:{RuntimeTag}";
    public bool IsWindows => Os == "windows";

    // Virtual properties for generic access (overridden by derived types that have them)
    public virtual string? RuntimeId => null;
    public virtual string? PackageName => null;
    public virtual string? PackageVersionSuffix => null;
    public virtual string? RelativeProfilerPath => null;
    public virtual string? RelativeApiWrapperPath => null;
}

public record InstallerScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record ChiseledScenario : SmokeTestScenario;

public record NuGetScenario : SmokeTestScenario
{
    public required string NuGetRuntimeId { get; init; }
    public override string? RuntimeId => NuGetRuntimeId;
    public override string? RelativeProfilerPath => $"datadog/{NuGetRuntimeId}/Datadog.Trace.ClrProfiler.Native.so";
    public override string? RelativeApiWrapperPath => $"datadog/{NuGetRuntimeId}/Datadog.Linux.ApiWrapper.x64.so";
}

public record DotnetToolScenario : SmokeTestScenario
{
    public required string DotnetToolRuntimeId { get; init; }
    public override string? RuntimeId => DotnetToolRuntimeId;
}

public record DotnetToolNugetScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.LinuxDotnetToolNuget;
    public required string DotnetToolNugetRuntimeId { get; init; }
    public override string? RuntimeId => DotnetToolNugetRuntimeId;
}

public record SelfInstrumentScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.LinuxSelfInstrument;
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record TrimmingScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public required string TrimmingRuntimeId { get; init; }
    public required string TrimmingPackageName { get; init; }
    public string? TrimmingPackageVersionSuffix { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
    public override string? RuntimeId => TrimmingRuntimeId;
    public override string? PackageName => TrimmingPackageName;
    public override string? PackageVersionSuffix => TrimmingPackageVersionSuffix;
}

public record WindowsMsiScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.WindowsMsi;
    public required string Channel32Bit { get; init; }
}

public record WindowsNuGetScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.WindowsNuGet;
    public required string Channel32Bit { get; init; }
    public required string WindowsRelativeProfilerPath { get; init; }
    public override string? RelativeProfilerPath => WindowsRelativeProfilerPath;
}

public record WindowsDotnetToolScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.WindowsDotnetTool;
    public required string Channel32Bit { get; init; }
}

public record WindowsTracerHomeScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.WindowsTracerHome;
    public required string Channel32Bit { get; init; }
    public required string WindowsRelativeProfilerPath { get; init; }
    public override string? RelativeProfilerPath => WindowsRelativeProfilerPath;
}

public record WindowsFleetInstallerIisScenario : SmokeTestScenario
{
    public override SmokeTestCategory Category => SmokeTestCategory.WindowsFleetInstallerIis;
    public required string TargetPlatform { get; init; }
    public required string FleetInstallerCommand { get; init; }
}
