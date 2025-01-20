using System;
using System.Collections.Generic;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;

namespace SmokeTests;

public static class SmokeTestBuilder
{
    const string DotnetSdkVersion = "9.0.100";
    public static SmokeTestCategory[] GetAllScenarios() => Enum.GetValues<SmokeTestCategory>();

    public static SmokeTestImage[] GetSmokeTestImagesForCategory(SmokeTestCategory category)
        => category switch
        {
            SmokeTestCategory.LinuxX64Installer => LinuxX64InstallerImages(),
            _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
        };

    static SmokeTestImage[] LinuxX64InstallerImages()
    {
        return new SmokeTestImage[]
        {
            // debian
            new("debian", TargetFramework.NET9_0, "9.0-noble"),
            new("debian", TargetFramework.NET9_0, "9.0-bookworm-slim"),
            new("debian", TargetFramework.NET8_0, "8.0-bookworm-slim"),
            new("debian", TargetFramework.NET8_0, "8.0-jammy"),
            new("debian", TargetFramework.NET7_0, "7.0-bullseye-slim"),
            new("debian", TargetFramework.NET6_0, "6.0-bullseye-slim"),
            new("debian", TargetFramework.NET5_0, "5.0-bullseye-slim"),
            new("debian", TargetFramework.NET5_0, "5.0-buster-slim"),
            new("debian", TargetFramework.NET5_0, "5.0-focal"),
            new("debian", TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
            new("debian", TargetFramework.NETCOREAPP3_1, "3.1-buster-slim"),
            new("debian", TargetFramework.NETCOREAPP3_1, "3.1-bionic"),
            new("debian", TargetFramework.NETCOREAPP2_1, "2.1-bionic"),
            new("debian", TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),

            // fedora
            new("fedora", TargetFramework.NET7_0, "35-7.0"),
            new("fedora", TargetFramework.NET6_0, "34-6.0"),
            new("fedora", TargetFramework.NET5_0, "35-5.0"),
            new("fedora", TargetFramework.NET5_0, "34-5.0"),
            new("fedora", TargetFramework.NET5_0, "33-5.0"),
            new("fedora", TargetFramework.NETCOREAPP3_1, "35-3.1"),
            new("fedora", TargetFramework.NETCOREAPP3_1, "34-3.1"),
            new("fedora", TargetFramework.NETCOREAPP3_1, "33-3.1"),
            new("fedora", TargetFramework.NETCOREAPP3_1, "29-3.1"),
            new("fedora", TargetFramework.NETCOREAPP2_1, "29-2.1"),

            // alpine
            new ("alpine", TargetFramework.NET9_0, "9.0-alpine3.20"),
            new ("alpine", TargetFramework.NET9_0, "9.0-alpine3.20-composite"),
            new ("alpine", TargetFramework.NET8_0, "8.0-alpine3.18"),
            new ("alpine", TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
            new ("alpine", TargetFramework.NET7_0, "7.0-alpine3.16"),
            new ("alpine", TargetFramework.NET6_0, "6.0-alpine3.16"),
            new ("alpine", TargetFramework.NET6_0, "6.0-alpine3.14"),
            new ("alpine", TargetFramework.NET5_0, "5.0-alpine3.14"),
            new ("alpine", TargetFramework.NET5_0, "5.0-alpine3.13"),
            new ("alpine", TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
            new ("alpine", TargetFramework.NETCOREAPP3_1, "3.1-alpine3.13"),
            new ("alpine", TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
        };
    }

    public static string BuildImage(SmokeTestCategory category, SmokeTestImage image, AbsolutePath tracerDir)
    {
        var dockerLogger = DockerTasks.DockerLogger;
        try
        {
            // avoid stderr being logged as an error
            DockerTasks.DockerLogger = (_, s) => Serilog.Log.Debug(s);

            return category switch
            {
                SmokeTestCategory.LinuxX64Installer => LinuxX64InstallerDockerfile(image, tracerDir),
                _ => throw new InvalidOperationException($"Unknown smoke test scenario: {category}"),
            };
        }
        finally
        {
            DockerTasks.DockerLogger = dockerLogger;
        }
    }

    static string LinuxX64InstallerDockerfile(SmokeTestImage image, AbsolutePath tracerDir)
    {
        var runtimeImage = $"mcr.microsoft.com/dotnet/aspnet:{image.RuntimeTag}";
        var installCmd = "dpkg -i ./datadog-dotnet-apm*_amd64.deb";
        var tag = $"dd-trace-dotnet/{image}-tester";

        DockerTasks.DockerBuild(
            x => x
                .SetPath(tracerDir)
                .SetFile(tracerDir / "build" / "_build" / "docker" / "smoke.dockerfile")
                .SetBuildArg($"DOTNETSDK_VERSION={DotnetSdkVersion}",
                             $"RUNTIME_IMAGE={runtimeImage}",
                             $"PUBLISH_FRAMEWORK={image.PublishFramework}",
                             $"INSTALL_CMD={installCmd}")
                .SetTag(tag));

        return tag;
    }
}

public enum SmokeTestCategory
{
    LinuxX64Installer,
    LinuxArm64Installer,
}

public class SmokeTestImage
{
    public SmokeTestImage(string shortName, string publishFramework, string runtimeTag, bool runCrashTest = true, bool isNoop = false)
    {
        ShortName = shortName;
        PublishFramework = publishFramework;
        RuntimeTag = runtimeTag;
        RunCrashTest = runCrashTest;
        IsNoop = isNoop;
    }

    public string ShortName { get; init; }
    public string PublishFramework { get; init; }
    public string RuntimeTag { get; init; }
    public bool RunCrashTest { get; init; }
    public bool IsNoop { get; init; }

    public override string ToString() => $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
}
