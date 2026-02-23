using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;

namespace SmokeTests;

public static class SmokeTestBuilder
{
    const string DotnetSdkVersion = "10.0.100";

    public static Dictionary<SmokeTestCategory, Dictionary<string, SmokeTestScenario>> GetAllScenarios()
        => Enum.GetValues<SmokeTestCategory>().ToDictionary(x => x, GetScenariosForCategory);

    static Dictionary<string, SmokeTestScenario> GetScenariosForCategory(SmokeTestCategory category)
    {
        var scenarios = category switch
        {
            SmokeTestCategory.LinuxX64Installer => LinuxX64InstallerScenarios(),
            _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
        };

        return scenarios.ToDictionary(x => x.JobName, x => x);

        static SmokeTestScenario[] LinuxX64InstallerScenarios()
        {
            return new SmokeTestScenario[]
            {
                // debian
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET9_0, "9.0-noble"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET9_0, "9.0-bookworm-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET8_0, "8.0-bookworm-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET8_0, "8.0-jammy"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET7_0, "7.0-bullseye-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET6_0, "6.0-bullseye-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET5_0, "5.0-bullseye-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET5_0, "5.0-buster-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NET5_0, "5.0-focal"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NETCOREAPP3_1, "3.1-buster-slim"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NETCOREAPP3_1, "3.1-bionic"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NETCOREAPP2_1, "2.1-bionic"),
                new(SmokeTestCategory.LinuxX64Installer, "debian", TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),

                // fedora
                // new("fedora", TargetFramework.NET7_0, "35-7.0"),
                // new("fedora", TargetFramework.NET6_0, "34-6.0"),
                // new("fedora", TargetFramework.NET5_0, "35-5.0"),
                // new("fedora", TargetFramework.NET5_0, "34-5.0"),
                // new("fedora", TargetFramework.NET5_0, "33-5.0"),
                // new("fedora", TargetFramework.NETCOREAPP3_1, "35-3.1"),
                // new("fedora", TargetFramework.NETCOREAPP3_1, "34-3.1"),
                // new("fedora", TargetFramework.NETCOREAPP3_1, "33-3.1"),
                // new("fedora", TargetFramework.NETCOREAPP3_1, "29-3.1"),
                // new("fedora", TargetFramework.NETCOREAPP2_1, "29-2.1"),
                //
                // // alpine
                // new ("alpine", TargetFramework.NET9_0, "9.0-alpine3.20"),
                // new ("alpine", TargetFramework.NET9_0, "9.0-alpine3.20-composite"),
                // new ("alpine", TargetFramework.NET8_0, "8.0-alpine3.18"),
                // new ("alpine", TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
                // new ("alpine", TargetFramework.NET7_0, "7.0-alpine3.16"),
                // new ("alpine", TargetFramework.NET6_0, "6.0-alpine3.16"),
                // new ("alpine", TargetFramework.NET6_0, "6.0-alpine3.14"),
                // new ("alpine", TargetFramework.NET5_0, "5.0-alpine3.14"),
                // new ("alpine", TargetFramework.NET5_0, "5.0-alpine3.13"),
                // new ("alpine", TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                // new ("alpine", TargetFramework.NETCOREAPP3_1, "3.1-alpine3.13"),
                // new ("alpine", TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
            };
        }
    }

    public static SmokeTestScenario GetScenario(SmokeTestCategory category, string scenario)
        => GetScenariosForCategory(category)[scenario];

    public static IReadOnlyCollection<Output> BuildImage(SmokeTestCategory category, SmokeTestScenario scenario, AbsolutePath tracerDir)
    {
        var dockerLogger = DockerTasks.DockerLogger;
        try
        {
            // avoid stderr being logged as an error
            DockerTasks.DockerLogger = (_, s) => Serilog.Log.Debug(s);

            return category switch
            {
                SmokeTestCategory.LinuxX64Installer => LinuxX64InstallerDockerfile(scenario, tracerDir),
                _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
            };
        }
        finally
        {
            DockerTasks.DockerLogger = dockerLogger;
        }

        static IReadOnlyCollection<Output> LinuxX64InstallerDockerfile(SmokeTestScenario scenario, AbsolutePath tracerDir)
        {
            var runtimeImage = $"mcr.microsoft.com/dotnet/aspnet:{scenario.RuntimeTag}";
            var installCmd = "dpkg -i ./datadog-dotnet-apm*_amd64.deb";

            return DockerTasks.DockerBuild(
                x => x
                    .SetPath(tracerDir)
                    .SetFile(tracerDir / "build" / "_build" / "docker" / "smoke.dockerfile")
                    .SetBuildArg($"DOTNETSDK_VERSION={DotnetSdkVersion}",
                                 $"RUNTIME_IMAGE={runtimeImage}",
                                 $"PUBLISH_FRAMEWORK={scenario.PublishFramework}",
                                 $"INSTALL_CMD={installCmd}")
                    .SetTag(scenario.DockerTag));
        }
    }
}

public enum SmokeTestCategory
{
    LinuxX64Installer,
    // LinuxArm64Installer,
}

public enum InstallType
{
    DebX64,
    RpmX64,
    TarX64,
    DebArm64,
    RpmArm64,
    TarArm64,
}

public enum Artifact
{
    LinuxX64,
    LinuxMuslX64,
    LinuxArm64,
}

public record SmokeTestScenario(
    SmokeTestCategory Category,
    string ShortName,
    string PublishFramework,
    string RuntimeTag,
    bool IsLinuxContainer = true,
    bool RunCrashTest = true,
    bool IsNoop = false)
{
    public string JobName { get; } = $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string FullName => $"{Category}_{JobName}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";
}
