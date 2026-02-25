using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public static class SmokeTestScenarios
{
    public static SmokeTestScenario GetScenario(SmokeTestCategory category, string scenario)
        => GetScenariosForCategory(category).First(x => x.JobName == scenario);

    public static Dictionary<SmokeTestCategory, Dictionary<string, SmokeTestScenario>> GetAllScenarios()
        => Enum.GetValues<SmokeTestCategory>()
            .ToDictionary(
                cat => cat,
                cat => GetScenariosForCategory(cat)
                    .ToDictionary(x => x.JobName, x => x));

    private static IEnumerable<SmokeTestScenario> GetScenariosForCategory(SmokeTestCategory category)
    {
        var scenarios = category switch
        {
            SmokeTestCategory.LinuxX64Installer => LinuxX64InstallerScenarios(),
            _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
        };

        return scenarios.SelectMany(x => x);

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxX64InstallerScenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-bionic"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-stretch-slim")
                });

            // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu_interim",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-ubuntu", "25.10-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-ubuntu", "25.04-9.0"),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "debian",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-debian", "trixie-9.0"),
                    (TargetFramework.NET8_0, "andrewlock/dotnet-debian", "trixie-8.0"),
                });

            // https://github.com/andrewlock/dotnet-docker-images (fedora)
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "fedora",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora", "42-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora", "40-9.0"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora", "35-7.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora", "34-6.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "35-5.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "34-5.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "33-5.0"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "35-3.1"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "34-3.1"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "33-3.1"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "29-3.1"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-fedora", "29-2.1"),
                });

            // Alpine tests with the default package
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "alpine",
                installType: InstallType.TarX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12"),
                });

            // Alpine tests with the musl-specific package
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "alpine_musl",
                installType: InstallType.TarMuslX64,
                artifactType: ArtifactType.LinuxMuslX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12"),
                });

            // centos
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET7_0, "andrewlock/dotnet-centos", "7-7.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos", "7-6.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos", "7-5.0"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos", "7-3.1"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-centos", "7-2.1"),
                });

            // rhel
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "rhel",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-rhel", "10-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "9-9.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "8-9.0"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-rhel", "8-7.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-rhel", "8-6.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-rhel", "8-5.0"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-rhel", "8-3.1"),
                });

            // centos-stream
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos-stream",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-centos-stream", "10-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-centos-stream", "9-9.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "9-6.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "8-6.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos-stream", "8-5.0"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos-stream", "8-3.1"),
                });

            // opensuse
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "opensuse",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-opensuse", "15-7.0"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-opensuse", "15-6.0"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-opensuse", "15-5.0"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-opensuse", "15-3.1"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-opensuse", "15-2.1"),
                });

            static IEnumerable<SmokeTestScenario> Get(
                SmokeTestCategory category,
                string shortName,
                InstallType installType,
                ArtifactType artifactType,
                params (string PublishFramework, string Image, string Tag)[] scenarios)
                => scenarios.Select(selector: scenario => new SmokeTestScenario(
                    Category: category,
                    ShortName: shortName,
                    PublishFramework: scenario.PublishFramework,
                    RuntimeTag: scenario.Tag,
                    DockerImageRepo: scenario.Image,
                    InstallType: installType,
                    ArtifactType: artifactType));
        }
    }
}
