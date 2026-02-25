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
    InstallType InstallType,
    ArtifactType ArtifactType,
    bool RunCrashTest = true,
    bool IsNoop = false)
{
    public string JobName { get; } = $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string FullName => $"{Category}_{JobName}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";

    public string RuntimeImage => $"{DockerImageRepo}:{RuntimeTag}";
    public string InstallCommand => InstallType.GetInstallCommand();
    public string ArtifactName => ArtifactType.GetArtifactName();
}
