using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public static class SmokeTestScenarios
{
    public static Dictionary<string, SmokeTestScenario> GetScenariosForCategory(SmokeTestCategory category)
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
        => SmokeTestScenarios.GetScenariosForCategory(category)[scenario];

    public static Dictionary<SmokeTestCategory, Dictionary<string, SmokeTestScenario>> GetAllScenarios()
        => Enum.GetValues<SmokeTestCategory>().ToDictionary(x => x, SmokeTestScenarios.GetScenariosForCategory);
}