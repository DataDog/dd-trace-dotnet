#nullable enable
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
            SmokeTestCategory.LinuxArm64Installer => LinuxArm64InstallerScenarios(),
            SmokeTestCategory.LinuxChiseledInstaller => LinuxChiseledInstallerScenarios(),
            SmokeTestCategory.LinuxChiseledArm64Installer => LinuxChiseledArm64InstallerScenarios(),
            SmokeTestCategory.LinuxNuGet => LinuxNuGetScenarios(),
            SmokeTestCategory.LinuxNuGetArm64 => LinuxNuGetArm64Scenarios(),
            SmokeTestCategory.LinuxDotnetTool => LinuxDotnetToolScenarios(),
            SmokeTestCategory.LinuxDotnetToolArm64 => LinuxDotnetToolArm64Scenarios(),
            SmokeTestCategory.LinuxDotnetToolNuget => LinuxDotnetToolNugetScenarios(),
            SmokeTestCategory.LinuxTrimming => LinuxTrimmingScenarios(),
            SmokeTestCategory.LinuxMuslInstaller => LinuxMuslInstallerScenarios(),
            SmokeTestCategory.LinuxMuslDotnetTool => LinuxMuslDotnetToolScenarios(),
            SmokeTestCategory.LinuxMuslDotnetToolArm64 => LinuxMuslDotnetToolArm64Scenarios(),
            SmokeTestCategory.LinuxMuslTrimming => LinuxMuslTrimmingScenarios(),
            SmokeTestCategory.LinuxSelfInstrument => LinuxSelfInstrumentScenarios(),
            SmokeTestCategory.WindowsMsi => WindowsMsiScenarios(),
            SmokeTestCategory.WindowsNuGet => WindowsNuGetScenarios(),
            SmokeTestCategory.WindowsDotnetTool => WindowsDotnetToolScenarios(),
            SmokeTestCategory.WindowsTracerHome => WindowsTracerHomeScenarios(),
            SmokeTestCategory.WindowsFleetInstaller => WindowsFleetInstallerScenarios(),
            _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
        };

        return scenarios.SelectMany(x => x);

        // ─────────────────────────────────────────────────────────
        // Installer categories (x64 + arm64)
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxX64InstallerScenarios()
        {
            // The dotnet/aspnet image is a mix of ubuntu and debian, but they're all in the same MS repository
            // So we split them here
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu",
                os: "ubuntu",
                installType: InstallType.DebX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-bionic", "bionic", true),
                });

            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim", "buster", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-stretch-slim", "stretch", true),
                });

            // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "ubuntu_interim",
                os: "ubuntu",
                installType: InstallType.DebX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-ubuntu", "25.10-10.0", "questing", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-ubuntu", "25.04-9.0", "plucky", true),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-debian", "trixie-9.0", "trixie", true),
                    (TargetFramework.NET8_0, "andrewlock/dotnet-debian", "trixie-8.0", "trixie", true),
                });

            // https://github.com/andrewlock/dotnet-docker-images (fedora)
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "fedora",
                os: "fedora",
                installType: InstallType.RpmX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora", "42-10.0", "42", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora", "40-9.0", "40", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora", "35-7.0", "35", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora", "34-6.0", "34", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "35-5.0", "35", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "34-5.0", "34", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "33-5.0", "33", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "35-3.1", "35", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "34-3.1", "34", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "33-3.1", "33", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "29-3.1", "29", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-fedora", "29-2.1", "29", true),
                });

            // Alpine tests with the default package
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "alpine",
                os: "alpine",
                installType: InstallType.TarX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13", "3.13", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12", true),
                });

            // centos
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos",
                os: "centos",
                installType: InstallType.RpmX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET7_0, "andrewlock/dotnet-centos", "7-7.0", "7", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos", "7-6.0", "7", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos", "7-5.0", "7", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos", "7-3.1", "7", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-centos", "7-2.1", "7", true),
                });

            // rhel
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "rhel",
                os: "rhel",
                installType: InstallType.RpmX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-rhel", "10-10.0", "10", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "9-9.0", "9", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "8-9.0", "8", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-rhel", "8-7.0", "8", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-rhel", "8-6.0", "8", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-rhel", "8-5.0", "8", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-rhel", "8-3.1", "8", true),
                });

            // centos-stream
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "centos-stream",
                os: "centos-stream",
                installType: InstallType.RpmX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-centos-stream", "10-10.0", "10", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-centos-stream", "9-9.0", "9", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "9-6.0", "9", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos-stream", "8-6.0", "8", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos-stream", "8-5.0", "8", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos-stream", "8-3.1", "8", true),
                });

            // opensuse
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxX64Installer,
                shortName: "opensuse",
                os: "opensuse",
                installType: InstallType.RpmX64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0", "15", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0", "15", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-opensuse", "15-7.0", "15", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-opensuse", "15-6.0", "15", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-opensuse", "15-5.0", "15", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-opensuse", "15-3.1", "15", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-opensuse", "15-2.1", "15", true),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxMuslInstallerScenarios()
        {
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxMuslInstaller,
                shortName: "alpine_musl",
                os: "alpine",
                installType: InstallType.TarMuslX64,
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.13", "3.13", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12", true),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxArm64InstallerScenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxArm64Installer,
                shortName: "ubuntu",
                os: "ubuntu",
                installType: InstallType.DebArm64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    // https://github.com/dotnet/runtime/issues/66707
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim", "buster", false),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", false),
                });

            // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxArm64Installer,
                shortName: "ubuntu_interim",
                os: "ubuntu",
                installType: InstallType.DebArm64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // new (publishFramework: TargetFramework.NET10_0, "25.10-10.0", "ubuntu", "questing"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-ubuntu", "25.04-9.0", "plucky", true),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxArm64Installer,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebArm64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-debian", "trixie-9.0", "trixie", true),
                    (TargetFramework.NET8_0, "andrewlock/dotnet-debian", "trixie-8.0", "trixie", true),
                });

            yield return GetInstaller(
                category: SmokeTestCategory.LinuxArm64Installer,
                shortName: "fedora",
                os: "fedora",
                installType: InstallType.RpmArm64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora-arm64", "42-10.0", "42", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora-arm64", "40-9.0", "40", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora-arm64", "35-7.0", "35", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora-arm64", "34-6.0", "34", true),
                    // https://github.com/dotnet/runtime/issues/66707
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora-arm64", "35-5.0", "35", false),
                });

            // Alpine tests with the default package
            yield return GetInstaller(
                category: SmokeTestCategory.LinuxArm64Installer,
                shortName: "alpine",
                os: "alpine",
                installType: InstallType.TarArm64,

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.19-composite", "3.19", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.18", "3.18", true),
                });
        }

        // ─────────────────────────────────────────────────────────
        // Chiseled categories
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxChiseledInstallerScenarios()
        {
            yield return GetChiseled(
                category: SmokeTestCategory.LinuxChiseledInstaller,
                shortName: "debian",
                os: "ubuntu",

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble-chiseled", "noble"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble-chiseled-composite", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble-chiseled", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble-chiseled-composite", "noble"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy-chiseled", "jammy"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy-chiseled-composite", "jammy"),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxChiseledArm64InstallerScenarios()
        {
            yield return GetChiseled(
                category: SmokeTestCategory.LinuxChiseledArm64Installer,
                shortName: "debian",
                os: "ubuntu",

                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble-chiseled", "noble"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble-chiseled-composite", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble-chiseled", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble-chiseled-composite", "noble"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy-chiseled", "jammy"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy-chiseled-composite", "jammy"),
                });
        }

        // ─────────────────────────────────────────────────────────
        // NuGet categories
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxNuGetScenarios()
        {
            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGet,
                shortName: "debian",
                os: "debian",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-bullseye-slim", "bullseye", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-stretch-slim", "stretch", true),
                });

            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGet,
                shortName: "fedora",
                os: "fedora",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora", "42-10.0", "42", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora", "40-9.0", "40", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora", "35-7.0", "35", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora", "34-6.0", "34", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "33-5.0", "33", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "35-3.1", "35", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-fedora", "29-2.1", "29", true),
                });

            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGet,
                shortName: "alpine",
                os: "alpine",
                runtimeId: "linux-musl-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12", true),
                });

            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGet,
                shortName: "centos",
                os: "centos",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET7_0, "andrewlock/dotnet-centos", "7-7.0", "7", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos", "7-6.0", "7", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos", "7-5.0", "7", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos", "7-3.1", "7", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-centos", "7-2.1", "7", true),
                });

            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGet,
                shortName: "opensuse",
                os: "opensuse",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0", "15", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0", "15", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-opensuse", "15-7.0", "15", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-opensuse", "15-6.0", "15", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-opensuse", "15-5.0", "15", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-opensuse", "15-3.1", "15", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-opensuse", "15-2.1", "15", true),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxNuGetArm64Scenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGetArm64,
                shortName: "ubuntu",
                os: "ubuntu",
                runtimeId: "linux-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim", "buster", false),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", false),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGetArm64,
                shortName: "debian",
                os: "debian",
                runtimeId: "linux-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie", true),
                });

            yield return GetNuGet(
                category: SmokeTestCategory.LinuxNuGetArm64,
                shortName: "alpine",
                os: "alpine",
                runtimeId: "linux-musl-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.19-composite", "3.19", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.18", "3.18", true),
                });
        }

        // ─────────────────────────────────────────────────────────
        // DotnetTool categories
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxDotnetToolScenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetTool,
                shortName: "ubuntu",
                os: "ubuntu",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-bullseye-slim", "bullseye", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-stretch-slim", "stretch", true),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetTool,
                shortName: "debian",
                os: "debian",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie", true),
                });

            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetTool,
                shortName: "fedora",
                os: "fedora",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-fedora", "42-10.0", "42", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-fedora", "40-9.0", "40", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-fedora", "35-7.0", "35", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-fedora", "34-6.0", "34", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-fedora", "33-5.0", "33", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-fedora", "35-3.1", "35", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-fedora", "29-2.1", "29", true),
                });

            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetTool,
                shortName: "centos",
                os: "centos",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET7_0, "andrewlock/dotnet-centos", "7-7.0", "7", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-centos", "7-6.0", "7", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-centos", "7-5.0", "7", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-centos", "7-3.1", "7", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-centos", "7-2.1", "7", true),
                });

            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetTool,
                shortName: "opensuse",
                os: "opensuse",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0", "15", true),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0", "15", true),
                    (TargetFramework.NET7_0, "andrewlock/dotnet-opensuse", "15-7.0", "15", true),
                    (TargetFramework.NET6_0, "andrewlock/dotnet-opensuse", "15-6.0", "15", true),
                    (TargetFramework.NET5_0, "andrewlock/dotnet-opensuse", "15-5.0", "15", true),
                    (TargetFramework.NETCOREAPP3_1, "andrewlock/dotnet-opensuse", "15-3.1", "15", true),
                    (TargetFramework.NETCOREAPP2_1, "andrewlock/dotnet-opensuse", "15-2.1", "15", true),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxDotnetToolArm64Scenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetToolArm64,
                shortName: "ubuntu",
                os: "ubuntu",
                runtimeId: "linux-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-buster-slim", "buster", false),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-focal", "focal", false),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetToolArm64,
                shortName: "debian",
                os: "debian",
                runtimeId: "linux-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie", true),
                });

        }

        // ─────────────────────────────────────────────────────────
        // Musl DotnetTool categories (Alpine)
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxMuslDotnetToolScenarios()
        {
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxMuslDotnetTool,
                shortName: "alpine",
                os: "alpine",
                runtimeId: "linux-musl-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.16", "3.16", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.14", "3.14", true),
                    (TargetFramework.NET5_0, "mcr.microsoft.com/dotnet/aspnet", "5.0-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/aspnet", "3.1-alpine3.14", "3.14", true),
                    (TargetFramework.NETCOREAPP2_1, "mcr.microsoft.com/dotnet/aspnet", "2.1-alpine3.12", "3.12", true),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxMuslDotnetToolArm64Scenarios()
        {
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxMuslDotnetToolArm64,
                shortName: "alpine",
                os: "alpine",
                runtimeId: "linux-musl-arm64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.19", "3.19", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.19-composite", "3.19", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/aspnet", "7.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/aspnet", "6.0-alpine3.18", "3.18", true),
                });
        }

        // ─────────────────────────────────────────────────────────
        // DotnetToolNuget category
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxDotnetToolNugetScenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            // Uses mcr.microsoft.com/dotnet/sdk (not aspnet!) as base image
            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetToolNuget,
                shortName: "ubuntu",
                os: "ubuntu",
                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/sdk", "10.0-noble", "noble", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/sdk", "9.0-bookworm-slim", "bookworm", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/sdk", "9.0-noble", "noble", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/sdk", "8.0-jammy", "jammy", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/sdk", "7.0-bullseye-slim", "bullseye", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/sdk", "6.0-bullseye-slim", "bullseye", true),
                    // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/sdk", "3.1-bullseye", "bullseye", true),
                },
                excludeWhenPrerelease: TargetFramework.NETCOREAPP3_1);

            yield return GetDotnetTool(
                category: SmokeTestCategory.LinuxDotnetToolNuget,
                shortName: "alpine",
                os: "alpine",
                runtimeId: "linux-musl-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/sdk", "10.0-alpine3.22", "3.22", true),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/sdk", "9.0-alpine3.20", "3.20", true),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/sdk", "8.0-alpine3.18", "3.18", true),
                    (TargetFramework.NET7_0, "mcr.microsoft.com/dotnet/sdk", "7.0-alpine3.16", "3.16", true),
                    (TargetFramework.NET6_0, "mcr.microsoft.com/dotnet/sdk", "6.0-alpine3.16", "3.16", true),
                    // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available
                    (TargetFramework.NETCOREAPP3_1, "mcr.microsoft.com/dotnet/sdk", "3.1-alpine3.15", "3.15", true),
                },
                excludeWhenPrerelease: TargetFramework.NETCOREAPP3_1);
        }

        // ─────────────────────────────────────────────────────────
        // Trimming category
        // ─────────────────────────────────────────────────────────

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxTrimmingScenarios()
        {
            // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
            yield return GetTrimming(
                category: SmokeTestCategory.LinuxTrimming,
                shortName: "ubuntu",
                os: "ubuntu",
                installType: InstallType.DebX64,

                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-noble", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-noble", "noble"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-bookworm-slim", "bookworm"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-jammy", "jammy"),
                });

            // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
            yield return GetTrimming(
                category: SmokeTestCategory.LinuxTrimming,
                shortName: "debian",
                os: "debian",
                installType: InstallType.DebX64,

                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-debian", "trixie-10.0", "trixie"),
                });

            yield return GetTrimming(
                category: SmokeTestCategory.LinuxTrimming,
                shortName: "rhel",
                os: "rhel",
                installType: InstallType.RpmX64,

                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    // (TargetFramework.NET10_0, "andrewlock/dotnet-rhel", "10-10.0", "10"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "9-9.0", "9"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-rhel", "8-9.0", "8"),
                });

            yield return GetTrimming(
                category: SmokeTestCategory.LinuxTrimming,
                shortName: "opensuse",
                os: "opensuse",
                installType: InstallType.RpmX64,

                runtimeId: "linux-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "andrewlock/dotnet-opensuse", "15-10.0", "15"),
                    (TargetFramework.NET9_0, "andrewlock/dotnet-opensuse", "15-9.0", "15"),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxMuslTrimmingScenarios()
        {
            yield return GetTrimming(
                category: SmokeTestCategory.LinuxMuslTrimming,
                shortName: "alpine_musl",
                os: "alpine",
                installType: InstallType.TarMuslX64,
                runtimeId: "linux-musl-x64",
                scenarios: new (string PublishFramework, string Image, string Tag, string OsVersion)[]
                {
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22", "3.22"),
                    (TargetFramework.NET10_0, "mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.22-composite", "3.22"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20", "3.20"),
                    (TargetFramework.NET9_0, "mcr.microsoft.com/dotnet/aspnet", "9.0-alpine3.20-composite", "3.20"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18", "3.18"),
                    (TargetFramework.NET8_0, "mcr.microsoft.com/dotnet/aspnet", "8.0-alpine3.18-composite", "3.18"),
                });
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> LinuxSelfInstrumentScenarios()
        {
            yield return new[]
            {
                new SmokeTestScenario(
                    Category: SmokeTestCategory.LinuxSelfInstrument,
                    ShortName: "debian",
                    PublishFramework: TargetFramework.NET6_0,
                    RuntimeTag: "6.0-bullseye-slim",
                    DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                    Os: "debian",
                    OsVersion: "bullseye",
                    InstallType: InstallType.DebX64,
                    RunCrashTest: true),
            };
        }

        // ─────────────────────────────────────────────────────────
        // Helper methods for creating scenarios
        // ─────────────────────────────────────────────────────────

        static IEnumerable<SmokeTestScenario> GetInstaller(
            SmokeTestCategory category,
            string shortName,
            string os,
            InstallType installType,
            params (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[] scenarios)
            => scenarios.Select(scenario => new SmokeTestScenario(
                Category: category,
                ShortName: shortName,
                PublishFramework: scenario.PublishFramework,
                RuntimeTag: scenario.Tag,
                DockerImageRepo: scenario.Image,
                InstallType: installType,
                Os: os,
                OsVersion: scenario.OsVersion,
                RunCrashTest: scenario.RunCrashTest));

        static IEnumerable<SmokeTestScenario> GetChiseled(
            SmokeTestCategory category,
            string shortName,
            string os,
            params (string PublishFramework, string Image, string Tag, string OsVersion)[] scenarios)
            => scenarios.Select(scenario => new SmokeTestScenario(
                Category: category,
                ShortName: shortName,
                PublishFramework: scenario.PublishFramework,
                RuntimeTag: scenario.Tag,
                DockerImageRepo: scenario.Image,
                Os: os,
                OsVersion: scenario.OsVersion));

        static IEnumerable<SmokeTestScenario> GetNuGet(
            SmokeTestCategory category,
            string shortName,
            string os,
            string runtimeId,
            params (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[] scenarios)
            => scenarios.Select(scenario => new SmokeTestScenario(
                Category: category,
                ShortName: shortName,
                PublishFramework: scenario.PublishFramework,
                RuntimeTag: scenario.Tag,
                DockerImageRepo: scenario.Image,
                RuntimeId: runtimeId,
                Os: os,
                OsVersion: scenario.OsVersion,
                RunCrashTest: scenario.RunCrashTest));

        static IEnumerable<SmokeTestScenario> GetDotnetTool(
            SmokeTestCategory category,
            string shortName,
            string os,
            string runtimeId,
            (string PublishFramework, string Image, string Tag, string OsVersion, bool RunCrashTest)[] scenarios,
            string? excludeWhenPrerelease = null)
            => scenarios.Select(scenario => new SmokeTestScenario(
                Category: category,
                ShortName: shortName,
                PublishFramework: scenario.PublishFramework,
                RuntimeTag: scenario.Tag,
                DockerImageRepo: scenario.Image,
                RuntimeId: runtimeId,
                Os: os,
                OsVersion: scenario.OsVersion,
                RunCrashTest: scenario.RunCrashTest,
                ExcludeWhenPrerelease: excludeWhenPrerelease is not null && scenario.PublishFramework == excludeWhenPrerelease));

        static IEnumerable<SmokeTestScenario> GetTrimming(
            SmokeTestCategory category,
            string shortName,
            string os,
            InstallType installType,
            string runtimeId,
            params (string PublishFramework, string Image, string Tag, string OsVersion)[] scenarios)
        {
            var packages = new[]
            {
                (name: "Datadog.Trace", suffix: "", packageShortName: "ddtrace"),
                (name: "Datadog.Trace.Trimming", suffix: "-prerelease", packageShortName: "ddtrace_trimming"),
            };

            return from scenario in scenarios
                   from package in packages
                   select new SmokeTestScenario(
                       Category: category,
                       ShortName: $"{package.packageShortName}_{shortName}",
                       PublishFramework: scenario.PublishFramework,
                       RuntimeTag: scenario.Tag,
                       DockerImageRepo: scenario.Image,
                       InstallType: installType,
                       RuntimeId: runtimeId,
                       PackageName: package.name,
                       PackageVersionSuffix: package.suffix,
                       Os: os,
                       OsVersion: scenario.OsVersion,
                       RunCrashTest: false);
        }

        // ─────────────────────────────────────────────────────────
        // Windows categories
        // ─────────────────────────────────────────────────────────

        static string GetInstallerChannel(string publishFramework) =>
            publishFramework.Replace("netcoreapp", string.Empty)
                            .Replace("net", string.Empty);

        static (TargetFramework PublishFramework, string Tag, string OsVersion)[] GetWindowsRuntimeImages() =>
            new []
            {
                (TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "servercore-2022"),
                (TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "servercore-2022"),
                (TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "servercore-2022"),
                (TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022", "servercore-2022"),
                (TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022", "servercore-2022"),
            };

        static IEnumerable<IEnumerable<SmokeTestScenario>> WindowsMsiScenarios()
        {
            // MSI: x64 with and without 32-bit runtime
            var platforms = new (string platform, bool enable32Bit)[]
            {
                ("x64", false),
                ("x64", true),
            };

            yield return from platform in platforms
                         from image in GetWindowsRuntimeImages()
                         let channel32Bit = platform.enable32Bit
                             ? GetInstallerChannel(image.PublishFramework)
                             : ""
                         select new SmokeTestScenario(
                             Category: SmokeTestCategory.WindowsMsi,
                             ShortName: $"{platform.platform}_{(platform.enable32Bit ? "32bit" : "64bit")}",
                             PublishFramework: image.PublishFramework,
                             RuntimeTag: image.Tag,
                             DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                             Os: "windows",
                             OsVersion: image.OsVersion,
                             RunCrashTest: false,
                             Channel32Bit: channel32Bit);
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> WindowsNuGetScenarios()
        {
            // NuGet: x64 and x86
            var platforms = new[] { "x64", "x86" };

            yield return from platform in platforms
                         from image in GetWindowsRuntimeImages()
                         let channel32Bit = platform == "x86"
                             ? GetInstallerChannel(image.PublishFramework)
                             : ""
                         select new SmokeTestScenario(
                             Category: SmokeTestCategory.WindowsNuGet,
                             ShortName: $"{platform}",
                             PublishFramework: image.PublishFramework,
                             RuntimeTag: image.Tag,
                             DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                             Os: "windows",
                             OsVersion: image.OsVersion,
                             RunCrashTest: false,
                             Channel32Bit: channel32Bit,
                             WindowsRelativeProfilerPath: $"datadog/win-{platform}/Datadog.Trace.ClrProfiler.Native.dll");
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> WindowsDotnetToolScenarios()
        {
            // DotnetTool: x64 and x86
            var platforms = new[] { "x64", "x86" };

            yield return from platform in platforms
                         from image in GetWindowsRuntimeImages()
                         let channel32Bit = platform == "x86"
                             ? GetInstallerChannel(image.PublishFramework)
                             : ""
                         select new SmokeTestScenario(
                             Category: SmokeTestCategory.WindowsDotnetTool,
                             ShortName: $"{platform}",
                             PublishFramework: image.PublishFramework,
                             RuntimeTag: image.Tag,
                             DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                             Os: "windows",
                             OsVersion: image.OsVersion,
                             RunCrashTest: false,
                             Channel32Bit: channel32Bit);
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> WindowsTracerHomeScenarios()
        {
            // TracerHome: x64 and x86
            var platforms = new[] { "x64", "x86" };

            yield return from platform in platforms
                         from image in GetWindowsRuntimeImages()
                         let channel32Bit = platform == "x86"
                             ? GetInstallerChannel(image.PublishFramework)
                             : ""
                         select new SmokeTestScenario(
                             Category: SmokeTestCategory.WindowsTracerHome,
                             ShortName: $"{platform}",
                             PublishFramework: image.PublishFramework,
                             RuntimeTag: image.Tag,
                             DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                             Os: "windows",
                             OsVersion: image.OsVersion,
                             RunCrashTest: false,
                             Channel32Bit: channel32Bit,
                             WindowsRelativeProfilerPath: $"win-{platform}/Datadog.Trace.ClrProfiler.Native.dll");
        }

        static IEnumerable<IEnumerable<SmokeTestScenario>> WindowsFleetInstallerScenarios()
        {
            // FleetInstaller: x64 and x86 (net10.0, net9.0, net8.0 only)
            var platforms = new[] { "x64", "x86" };
            var images = GetWindowsRuntimeImages()
                .Where(x => x.PublishFramework.IsGreaterThan(TargetFramework.NET8_0));

            yield return from platform in platforms
                         from image in images
                         let channel32Bit = platform == "x86"
                             ? GetInstallerChannel(image.PublishFramework)
                             : ""
                         select new SmokeTestScenario(
                             Category: SmokeTestCategory.WindowsFleetInstaller,
                             ShortName: $"{platform}",
                             PublishFramework: image.PublishFramework,
                             RuntimeTag: image.Tag,
                             DockerImageRepo: "mcr.microsoft.com/dotnet/aspnet",
                             Os: "windows",
                             OsVersion: image.OsVersion,
                             RunCrashTest: false,
                             Channel32Bit: channel32Bit);
        }
    }
}
