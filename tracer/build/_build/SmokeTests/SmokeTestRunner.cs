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

    public static async Task BuildImageAsync(SmokeTestCategory category, SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, CancellationToken cancellationToken = default)
    {
        switch (category)
        {
            case SmokeTestCategory.LinuxX64Installer:
                await BuildLinuxX64InstallerImageAsync(scenario, tracerDir, artifactsDir, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unknown smoke test scenario: {category}");
        }
    }

    static async Task BuildLinuxX64InstallerImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, CancellationToken cancellationToken)
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

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir, cancellationToken);
    }

    static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) };
    static int MaxRetries => RetryDelays.Length;

    static async Task BuildImageFromDockerfileAsync(
        AbsolutePath contextDir,
        string dockerfilePath,
        string tag,
        Dictionary<string, string> buildArgs,
        AbsolutePath artifactsDir,
        CancellationToken cancellationToken)
    {
        // Build the context tar once — MemoryStream is re-seekable for retries
        using var contextStream = CreateBuildContextTar(contextDir, dockerfilePath, artifactsDir);

        var buildParams = new ImageBuildParameters
        {
            Dockerfile = dockerfilePath,
            Tags = new List<string> { tag },
            BuildArgs = buildArgs,
            Remove = true,
            ForceRemove = true,
        };

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // Reset the context stream for each attempt
                contextStream.Position = 0;

                // Create a fresh client for each attempt — the previous connection may be in a bad state
                using var client = CreateDockerClient();

                string lastError = null;
                var progress = new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Stream))
                    {
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

                Logger.Information("Building image {Tag} using Docker API (attempt {Attempt}/{MaxRetries})...", tag, attempt, MaxRetries);

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
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = RetryDelays[attempt - 1];
                Logger.Warning(ex, "Docker build attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s...", attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
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
    /// The Dockerfile COPYs the test app directory into the builder stage, including an
    /// "artifacts" subdirectory containing installer packages (.deb/.rpm). In CI, these
    /// are downloaded separately, so we inject them into the tar at the expected path.
    /// </summary>
    static MemoryStream CreateBuildContextTar(AbsolutePath contextDir, string dockerfilePath, AbsolutePath artifactsDir)
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

            // Inject installer artifacts into the test app's artifacts/ subdirectory.
            // The Dockerfile does: COPY --from=builder /src/artifacts /app/install
            // Since the test app is COPYd to /src, the artifacts must be at /src/artifacts,
            // i.e. inside the test app directory as "artifacts/".
            if (Directory.Exists(artifactsDir))
            {
                var artifactsTarPath = $"{testAppRelPath}/artifacts";
                var artifactFiles = Directory.GetFiles(artifactsDir, "*", SearchOption.AllDirectories);
                Logger.Information("Injecting {Count} artifact files from {ArtifactsDir} into tar at {TarPath}", artifactFiles.Length, artifactsDir, artifactsTarPath);
                foreach (var f in artifactFiles)
                {
                    Logger.Information("  Artifact: {File}", Path.GetRelativePath(artifactsDir, f));
                }

                AddDirectoryToTar(tarWriter, artifactsDir, artifactsTarPath);
            }
            else
            {
                Logger.Warning("Artifacts directory {ArtifactsDir} does not exist — image build will likely fail at COPY --from=builder /src/artifacts", artifactsDir);
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
