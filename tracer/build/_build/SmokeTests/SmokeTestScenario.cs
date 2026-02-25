#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public record SmokeTestScenario(
    SmokeTestCategory Category,
    string ShortName,
    string PublishFramework,
    string RuntimeTag,
    string DockerImageRepo,
    string Os,
    string OsVersion,
    InstallType? InstallType = null,
    ArtifactType? ArtifactType = null,
    string? RuntimeId = null,
    string? PackageName = null,
    string? PackageVersionSuffix = null,
    bool ExcludeWhenPrerelease = false,
    bool RunCrashTest = true,
    bool IsNoop = false)
{
    public string JobName { get; } = $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string FullName => $"{Category}_{JobName}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";

    public string RuntimeImage => $"{DockerImageRepo}:{RuntimeTag}";
    public string? InstallCommand => InstallType?.GetInstallCommand();
    public string? ArtifactName => ArtifactType?.GetArtifactName();

    public string? RelativeProfilerPath => RuntimeId is not null
        ? $"datadog/{RuntimeId}/Datadog.Trace.ClrProfiler.Native.so" : null;
    public string? RelativeApiWrapperPath => RuntimeId is not null
        ? $"datadog/{RuntimeId}/Datadog.Linux.ApiWrapper.x64.so" : null;
}
