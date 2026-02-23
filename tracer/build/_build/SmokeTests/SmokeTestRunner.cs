using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Nuke.Common.IO;
using Logger = Serilog.Log;

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

    public static async Task BuildImageAsync(SmokeTestCategory category, SmokeTestScenario scenario, AbsolutePath tracerDir, CancellationToken cancellationToken = default)
    {
        switch (category)
        {
            case SmokeTestCategory.LinuxX64Installer:
                await BuildLinuxX64InstallerImageAsync(scenario, tracerDir, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unknown smoke test scenario: {category}");
        }
    }

    static async Task BuildLinuxX64InstallerImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, CancellationToken cancellationToken)
    {
        var runtimeImage = $"mcr.microsoft.com/dotnet/aspnet:{scenario.RuntimeTag}";
        var installCmd = "dpkg -i ./datadog-dotnet-apm*_amd64.deb";
        var dockerfilePath = "build/_build/docker/smoke.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = runtimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["INSTALL_CMD"] = installCmd,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, cancellationToken);
    }

    static async Task BuildImageFromDockerfileAsync(
        AbsolutePath contextDir,
        string dockerfilePath,
        string tag,
        Dictionary<string, string> buildArgs,
        CancellationToken cancellationToken)
    {
        using var client = CreateDockerClient();

        // Create a tar archive of the build context
        using var contextStream = CreateBuildContextTar(contextDir, dockerfilePath);

        var buildParams = new ImageBuildParameters
        {
            Dockerfile = dockerfilePath,
            Tags = new List<string> { tag },
            BuildArgs = buildArgs,
            Remove = true,
            ForceRemove = true,
        };

        string lastError = null;
        var progress = new Progress<JSONMessage>(msg =>
        {
            if (!string.IsNullOrEmpty(msg.Stream))
            {
                // Stream lines already contain trailing newlines
                var line = msg.Stream.TrimEnd('\n', '\r');
                if (!string.IsNullOrEmpty(line))
                {
                    Logger.Debug("{DockerBuild}", line);
                }
            }

            if (!string.IsNullOrEmpty(msg.Status))
            {
                Logger.Debug("[{Id}] {Status} {Progress}", msg.ID, msg.Status, msg.ProgressMessage);
            }

            if (!string.IsNullOrEmpty(msg.ErrorMessage))
            {
                lastError = msg.ErrorMessage;
                Logger.Error("Docker build error: {Error}", msg.ErrorMessage);
            }
        });

        Logger.Information("Building image {Tag} using Docker API...", tag);

        await client.Images.BuildImageFromDockerfileAsync(
            buildParams,
            contextStream,
            authConfigs: null,
            headers: null,
            progress: progress,
            cancellationToken: cancellationToken);

        if (lastError is not null)
        {
            throw new InvalidOperationException($"Docker build failed: {lastError}");
        }

        Logger.Information("Successfully built image {Tag}", tag);
    }

    static DockerClient CreateDockerClient()
    {
        // Use the default Docker endpoint for the current platform
        var endpoint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        return new DockerClientConfiguration(endpoint).CreateClient();
    }

    /// <summary>
    /// Creates a tar archive containing only the files needed for the Docker build context.
    /// This avoids sending the entire tracer directory (which would be huge).
    /// </summary>
    static MemoryStream CreateBuildContextTar(AbsolutePath contextDir, string dockerfilePath)
    {
        var memoryStream = new MemoryStream();
        using (var tarWriter = new TarWriter(memoryStream, leaveOpen: true))
        {
            // Add the Dockerfile
            var fullDockerfilePath = contextDir / dockerfilePath;
            tarWriter.WriteEntry(fullDockerfilePath, dockerfilePath.Replace('\\', '/'));

            // Add the test application directory (referenced by COPY in the Dockerfile)
            var testAppRelPath = "test/test-applications/regression/AspNetCoreSmokeTest";
            var testAppDir = contextDir / testAppRelPath;
            if (Directory.Exists(testAppDir))
            {
                AddDirectoryToTar(tarWriter, testAppDir, testAppRelPath);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    static void AddDirectoryToTar(TarWriter tarWriter, string sourceDir, string tarBasePath)
    {
        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            // Compute relative path within tar, always using forward slashes
            var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
            var entryName = $"{tarBasePath}/{relativePath}";
            tarWriter.WriteEntry(filePath, entryName);
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
