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
            // The dotnet/aspnet image is a mix of ubuntu and debian, but they're all in the same MS repository
            // So we split them here
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu",
                os: "ubuntu",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-bionic", "bionic"),
                });

            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim", "buster"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-stretch-slim", "stretch"),
                });

            // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu_interim",
                os: "ubuntu",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-ubuntu", "25.10-10.0", "questing"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-ubuntu", "25.04-9.0", "plucky"),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-debian", "trixie-9.0", "trixie"),
                    (TargetFramework.NET8_0, "andrewlock/dotnet-debian", "trixie-8.0", "trixie"),
                });

            // https://github.com/andrewlock/dotnet-docker-images (fedora)
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "fedora",
                os: "fedora",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora", "42-10.0", "42"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora", "40-9.0", "40"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora", "35-7.0", "35"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora", "34-6.0", "34"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "35-5.0", "35"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "34-5.0", "34"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "33-5.0", "33"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "35-3.1", "35"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "34-3.1", "34"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "33-3.1", "33"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "29-3.1", "29"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-fedora", "29-2.1", "29"),
                });

            // Alpine tests with the default package
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "alpine",
                os: "alpine",
                installType: InstallType.TarX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13", "3.13"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12"),
                });

            // Alpine tests with the musl-specific package
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "alpine_musl",
                os: "alpine",
                installType: InstallType.TarMuslX64,
                artifactType: ArtifactType.LinuxMuslX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18"),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16"),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14"),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13", "3.13"),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14"),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12"),
                });

            // centos
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos",
                os: "centos",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET7_0, "andrewlock/dotnet-centos", "7-7.0", "7"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos", "7-6.0", "7"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos", "7-5.0", "7"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos", "7-3.1", "7"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-centos", "7-2.1", "7"),
                });

            // rhel
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "rhel",
                os: "rhel",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-rhel", "10-10.0", "10"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "9-9.0", "9"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "8-9.0", "8"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-rhel", "8-7.0", "8"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-rhel", "8-6.0", "8"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-rhel", "8-5.0", "8"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-rhel", "8-3.1", "8"),
                });

            // centos-stream
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos-stream",
                os: "centos-stream",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-centos-stream", "10-10.0", "10"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-centos-stream", "9-9.0", "9"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "9-6.0", "9"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "8-6.0", "8"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos-stream", "8-5.0", "8"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos-stream", "8-3.1", "8"),
                });

            // opensuse
            yield return Get(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "opensuse",
                os: "opensuse",
                installType: InstallType.RpmX64,
                artifactType: ArtifactType.LinuxX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0", "15"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0", "15"),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-opensuse", "15-7.0", "15"),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-opensuse", "15-6.0", "15"),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-opensuse", "15-5.0", "15"),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-opensuse", "15-3.1", "15"),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-opensuse", "15-2.1", "15"),
                });

            static IEnumerable<SmokeTestScenario> Get(
                SmokeTestCategory category,
                string shortName,
                string os,
                InstallType installType,
                ArtifactType artifactType,
                params (string PublishFramework, string Image, string Tag, string OsVersion)[] scenarios)
                => scenarios.Select(selector: scenario => new SmokeTestScenario(
                    Category: category,
                    ShortName: shortName,
                    PublishFramework: scenario.PublishFramework,
                    RuntimeTag: scenario.Tag,
                    DockerImageRepo: scenario.Image,
                    InstallType: installType,
                    ArtifactType: artifactType,
                    Os: os,
                    OsVersion: scenario.OsVersion));
        }
    }
}
