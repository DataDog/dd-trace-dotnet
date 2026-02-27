using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

namespace SmokeTests;

public static class SmokeTestBuilder
{
    const string DotnetSdkVersion = "10.0.100";
    const string TestAgentImage = "ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest";
    const string WindowsTestAgentImage = "dd-trace-dotnet/ddapm-test-agent-windows";
    const string WindowsTestAgentDockerfile = "build/_build/docker/test-agent.windows.dockerfile";

    const string SnapshotIgnoredAttrs =
        "span_id" + ",trace_id" + ",parent_id" + ",duration" + ",start" + ",metrics.system.pid" + ",meta.runtime-id" + ","
      + "meta.network.client.ip" + ",meta.http.client_ip" + ",metrics.process_id" + ",meta._dd.p.dm" + ","
      + "meta._dd.p.tid" + ",meta._dd.parent_id" + ",meta._dd.appsec.s.req.params" + ","
      + "meta._dd.appsec.s.res.body" + ",meta._dd.appsec.s.req.headers" + ","
      + "meta._dd.appsec.s.res.headers" + ",meta._dd.appsec.fp.http.endpoint" + ","
      + "meta._dd.appsec.fp.http.header" + ",meta._dd.appsec.fp.http.network";

    static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) };
    static int MaxRetries => RetryDelays.Length;

    // ──────────────────────────────────────────────────────────────
    // Image build
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds all Docker images for the given scenario. Returns the list of image tags to test.
    /// Most categories produce a single image; chiseled categories produce two (one per entrypoint style).
    /// </summary>
    public static async Task<string[]> BuildImageAsync(SmokeTestCategory category, SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion)
    {
        LogSection($"Building image: {scenario.ShortName}");
        Logger.Information("Artifacts: {ArtifactsDir}", artifactsDir);

        switch (category)
        {
            case SmokeTestCategory.LinuxX64Installer:
            case SmokeTestCategory.LinuxArm64Installer:
            case SmokeTestCategory.LinuxMuslInstaller:
                return await BuildInstallerImageAsync(scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.LinuxChiseledInstaller:
            case SmokeTestCategory.LinuxChiseledArm64Installer:
                return await BuildChiseledImageAsync(category, scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.LinuxNuGet:
            case SmokeTestCategory.LinuxNuGetArm64:
                return await BuildNuGetImageAsync(scenario, tracerDir, artifactsDir, toolVersion);
            case SmokeTestCategory.LinuxDotnetTool:
            case SmokeTestCategory.LinuxDotnetToolArm64:
            case SmokeTestCategory.LinuxMuslDotnetTool:
            case SmokeTestCategory.LinuxMuslDotnetToolArm64:
                return await BuildDotnetToolImageAsync(scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.LinuxDotnetToolNuget:
                return await BuildDotnetToolNugetImageAsync(scenario, tracerDir, artifactsDir, toolVersion);
            case SmokeTestCategory.LinuxTrimming:
            case SmokeTestCategory.LinuxMuslTrimming:
                return await BuildTrimmingImageAsync(scenario, tracerDir, artifactsDir, toolVersion);
            case SmokeTestCategory.WindowsMsi:
                return await BuildWindowsMsiImageAsync(scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.WindowsNuGet:
                return await BuildWindowsNuGetImageAsync(scenario, tracerDir, artifactsDir, toolVersion);
            case SmokeTestCategory.WindowsDotnetTool:
                return await BuildWindowsDotnetToolImageAsync(scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.WindowsTracerHome:
                return await BuildWindowsTracerHomeImageAsync(scenario, tracerDir, artifactsDir);
            case SmokeTestCategory.WindowsFleetInstaller:
                return await BuildWindowsFleetInstallerImageAsync(scenario, tracerDir, artifactsDir);
            default:
                throw new InvalidOperationException($"Unknown smoke test scenario: {category}");
        }
    }

    static async Task<string[]> BuildInstallerImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        const string dockerfilePath = "build/_build/docker/smoke.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["INSTALL_CMD"] = scenario.InstallCommand!,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

        return new[] { scenario.DockerTag };
    }

    static async Task<string[]> BuildChiseledImageAsync(SmokeTestCategory category, SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        const string dockerfilePath = "build/_build/docker/smoke.chiseled.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
        };

        // Build the "manual" env-var-based entrypoint image
        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir, target: "installer-final");

        // Build the dd-dotnet entrypoint image (reuses cached layers through installer-base)
        var ddDotnetTarget = category switch
        {
            SmokeTestCategory.LinuxChiseledInstaller => "dd-dotnet-final-linux-x64",
            SmokeTestCategory.LinuxChiseledArm64Installer => "dd-dotnet-final-linux-arm64",
            _ => throw new InvalidOperationException($"Unexpected category for {nameof(BuildChiseledImageAsync)}: {category}"),
        };

        var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, ddDotnetTag, buildArgs, artifactsDir, target: ddDotnetTarget);

        return new[] { scenario.DockerTag, ddDotnetTag };
    }

    static async Task<string[]> BuildNuGetImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion)
    {
        const string dockerfilePath = "build/_build/docker/smoke.nuget.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["TOOL_VERSION"] = toolVersion,
            ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath!,
            ["RELATIVE_APIWRAPPER_PATH"] = scenario.RelativeApiWrapperPath!,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] {scenario.DockerTag};
    }

    static async Task<string[]> BuildDotnetToolImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        const string dockerfilePath = "build/_build/docker/smoke.dotnet-tool.dockerfile";

        var installCmd = $"./datadog-dotnet-apm-*/{scenario.RuntimeId}/createLogPath.sh && cp -r ./datadog-dotnet-apm-*/{scenario.RuntimeId} /opt/datadog";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["INSTALL_CMD"] = installCmd,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] {scenario.DockerTag};
    }

    static async Task<string[]> BuildDotnetToolNugetImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion)
    {
        const string dockerfilePath = "build/_build/docker/smoke.dotnet-tool.nuget.dockerfile";

        var installCmd = $"./datadog-dotnet-apm-*/{scenario.RuntimeId}/createLogPath.sh && cp -r ./datadog-dotnet-apm-*/{scenario.RuntimeId} /opt/datadog";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["INSTALL_CMD"] = installCmd,
            ["TOOL_VERSION"] = toolVersion,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] {scenario.DockerTag};
    }

    static async Task<string[]> BuildTrimmingImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion)
    {
        const string dockerfilePath = "build/_build/docker/smoke.trimming.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["INSTALL_CMD"] = scenario.InstallCommand!,
            ["TOOL_VERSION"] = toolVersion + (scenario.PackageVersionSuffix ?? ""),
            ["PACKAGE_NAME"] = scenario.PackageName!,
            ["RUNTIME_IDENTIFIER"] = scenario.RuntimeId!,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

        return new[] {scenario.DockerTag};
    }

    // ──────────────────────────────────────────────────────────────
    // Windows image builds
    // ──────────────────────────────────────────────────────────────

    static async Task BuildWindowsTestAgentImageAsync(AbsolutePath tracerDir)
    {
        var buildArgs = new Dictionary<string, string>();
        await BuildImageFromDockerfileAsync(tracerDir, WindowsTestAgentDockerfile, WindowsTestAgentImage, buildArgs, null);
    }

    static async Task<string[]> BuildWindowsMsiImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        // The Dockerfile expects the MSI file to be named "datadog-apm.msi"
        // Rename any *.msi file in the artifacts directory
        RenameArtifact(artifactsDir, "*.msi", "datadog-apm.msi");

        // Build the standard MSI image
        const string dockerfilePath = "build/_build/docker/smoke.windows.dockerfile";
        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

        // Build the dd-dotnet variant (always uses no 32-bit)
        const string ddDotnetDockerfilePath = "build/_build/docker/smoke.windows.dd-dotnet.dockerfile";
        var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
        var ddDotnetBuildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["CHANNEL_32_BIT"] = "",
        };

        await BuildImageFromDockerfileAsync(tracerDir, ddDotnetDockerfilePath, ddDotnetTag, ddDotnetBuildArgs, artifactsDir);

        return new[] { scenario.DockerTag, ddDotnetTag };
    }

    static async Task<string[]> BuildWindowsNuGetImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion)
    {
        // Build the standard NuGet image
        const string dockerfilePath = "build/_build/docker/smoke.windows.nuget.dockerfile";
        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["TOOL_VERSION"] = toolVersion,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
            ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath!,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

        // Build the dd-dotnet NuGet variant
        const string ddDotnetDockerfilePath = "build/_build/docker/smoke.windows.nuget.dd-dotnet.dockerfile";
        var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
        var ddDotnetBuildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["TOOL_VERSION"] = toolVersion,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
        };

        await BuildImageFromDockerfileAsync(tracerDir, ddDotnetDockerfilePath, ddDotnetTag, ddDotnetBuildArgs, artifactsDir);

        return new[] { scenario.DockerTag, ddDotnetTag };
    }

    static async Task<string[]> BuildWindowsDotnetToolImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        // The Dockerfile expects "dd-trace-win.zip"
        RenameArtifact(artifactsDir, "dd-trace-win-*.zip", "dd-trace-win.zip");

        const string dockerfilePath = "build/_build/docker/smoke.windows.dotnet-tool.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] { scenario.DockerTag };
    }

    static async Task<string[]> BuildWindowsTracerHomeImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        const string dockerfilePath = "build/_build/docker/smoke.windows.tracer-home.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
            ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath!,
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] { scenario.DockerTag };
    }

    static async Task<string[]> BuildWindowsFleetInstallerImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir)
    {
        // The Dockerfile expects installer executables in an "installer/" subdirectory
        // Move any fleet installer exe/dll files into the subdirectory
        if (artifactsDir is not null && Directory.Exists(artifactsDir))
        {
            var installerDir = Path.Combine(artifactsDir, "installer");
            if (!Directory.Exists(installerDir))
            {
                Directory.CreateDirectory(installerDir);
                foreach (var file in Directory.GetFiles(artifactsDir, "Datadog.FleetInstaller*"))
                {
                    var dest = Path.Combine(installerDir, Path.GetFileName(file));
                    Logger.Information("Moving {Source} -> {Dest}", Path.GetFileName(file), $"installer/{Path.GetFileName(file)}");
                    File.Move(file, dest);
                }
            }
        }

        const string dockerfilePath = "build/_build/docker/smoke.windows.fleet-installer.dockerfile";

        var buildArgs = new Dictionary<string, string>
        {
            ["DOTNETSDK_VERSION"] = DotnetSdkVersion,
            ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
            ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
            ["CHANNEL_32_BIT"] = scenario.Channel32Bit ?? "",
        };

        await BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
        return new[] { scenario.DockerTag };
    }

    // ──────────────────────────────────────────────────────────────
    // Smoke test orchestration
    // ──────────────────────────────────────────────────────────────

    public static async Task RunSmokeTestAsync(
        SmokeTestScenario scenario,
        AbsolutePath tracerDir,
        AbsolutePath buildDataDir,
        string imageTag)
    {
        LogSection($"Running smoke test: {imageTag}");

        var networkName = $"smoke-test-{Guid.NewGuid():N}";
        string testAgentContainerId = null;
        string smokeTestContainerId = null;
        string crashTestContainerId = null;
        string buildContainerId = null;

        // Ensure output directories exist
        // debugSnapshotsDir: mounted as /debug_snapshots in the test-agent container,
        // also where we write dumped traces/stats/requests from the host
        var debugSnapshotsDir = buildDataDir / "snapshots";
        var logsDir = buildDataDir / "logs";
        var dumpsDir = buildDataDir / "dumps";
        EnsureExistingDirectory(debugSnapshotsDir);
        EnsureExistingDirectory(logsDir);
        EnsureExistingDirectory(dumpsDir);

        // Make bind-mounted directories world-writable so non-root containers
        // (e.g. chiseled images) can write logs and dumps
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const UnixFileMode worldRwx =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(debugSnapshotsDir, worldRwx);
            File.SetUnixFileMode(logsDir, worldRwx);
            File.SetUnixFileMode(dumpsDir, worldRwx);
        }

        using var client = CreateDockerClient();

        try
        {
            // 1. Create a dedicated Docker network
            await RetryAsync(
                "Create Docker network",
                async () =>
                {
                    Logger.Information("Creating Docker network {Network}...", networkName);
                    await client.Networks.CreateNetworkAsync(new NetworksCreateParameters { Name = networkName });
                },
                RetryDelays);

            // 2. Detect environment (container vs host) for path translation & networking
            //    Must happen before creating containers, because bind mount paths need
            //    translating when running inside a container (Docker-in-Docker).
            var environment = await DetectEnvironmentAsync(client, networkName);
            buildContainerId = environment.BuildContainerId;

            // 3. Pull/build + start test-agent container
            LogSection("Starting test agent");
            var sourceSnapshotsDir = tracerDir / "build" / "smoke_test_snapshots";

            if (scenario.IsWindows)
            {
                await BuildWindowsTestAgentImageAsync(tracerDir);
                testAgentContainerId = await CreateAndStartContainerWithRetryAsync(
                    client, "test-agent", BuildWindowsTestAgentContainerParams(
                        networkName,
                        environment.ToHostPath(sourceSnapshotsDir),
                        environment.ToHostPath(debugSnapshotsDir)));
            }
            else
            {
                await PullImageAsync(client, TestAgentImage);
                testAgentContainerId = await CreateAndStartContainerWithRetryAsync(
                    client, "test-agent", BuildTestAgentContainerParams(
                        networkName,
                        environment.ToHostPath(sourceSnapshotsDir),
                        environment.ToHostPath(debugSnapshotsDir)));
            }

            // 4. Determine how to reach the test-agent's HTTP API
            var testAgentBaseUrl = await GetTestAgentUrlAsync(client, environment, testAgentContainerId);
            Logger.Information("Test agent reachable at {Url}", testAgentBaseUrl);

            // 5. Wait for test-agent to be healthy
            using var httpClient = new HttpClient { BaseAddress = new Uri(testAgentBaseUrl) };
            await WaitForTestAgentAsync(httpClient);

            // 6. Start a trace session
            var sessionToken = Guid.NewGuid().ToString();
            Logger.Information("Starting trace session {Token}...", sessionToken);
            await RetryAsync(
                "Start trace session",
                async () =>
                {
                    var startResponse = await httpClient.GetAsync(
                        $"/test/session/start?test_session_token={sessionToken}");
                    startResponse.EnsureSuccessStatusCode();
                },
                RetryDelays);

            // 7. Start the smoke test app container, wait for exit
            LogSection("Running smoke test app");
            var appContainerParams = scenario.IsWindows
                ? BuildWindowsSmokeTestAppContainerParams(
                    imageTag, networkName,
                    environment.ToHostPath(logsDir))
                : BuildSmokeTestAppContainerParams(
                    imageTag, networkName,
                    environment.ToHostPath(logsDir),
                    environment.ToHostPath(dumpsDir));
            smokeTestContainerId = await CreateAndStartContainerWithRetryAsync(
                client, "smoke-test", appContainerParams);

            Logger.Information("Waiting for smoke test container to exit...");
            using (var appTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(15)))
            {
                var waitResponse = await client.Containers.WaitContainerAsync(smokeTestContainerId, appTimeoutCts.Token);
                Logger.Information("Smoke test container exited with code {Code}", waitResponse.StatusCode);

                if (waitResponse.StatusCode != 0)
                {
                    throw new InvalidOperationException($"Smoke test container exited with code {waitResponse.StatusCode}");
                }
            }

            // 8. Dump traces/stats/requests from test-agent
            LogSection("Verifying results");
            await DumpSessionDataAsync(httpClient, sessionToken, "smoke_test", debugSnapshotsDir);

            // 9. Verify snapshot
            if (!scenario.IsNoop)
            {
                var snapshotFile = scenario.PublishFramework == "netcoreapp2.1"
                    ? "smoke_test_snapshots_2_1"
                    : "smoke_test_snapshots";

                Logger.Information("Verifying snapshot {File}...", snapshotFile);

                // Windows containers mount snapshots at c:/snapshots, Linux at /snapshots
                var snapshotPathPrefix = scenario.IsWindows ? "c:/snapshots" : "/snapshots";

                // Retry the HTTP call itself (transport errors), but not a successful
                // response indicating a real snapshot mismatch
                var verifyResponse = await RetryAsync(
                    $"Verify snapshot {snapshotFile}",
                    () => httpClient.GetAsync(
                        $"/test/session/snapshot?test_session_token={sessionToken}&file={snapshotPathPrefix}/{snapshotFile}"),
                    RetryDelays);

                var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
                if (!verifyResponse.IsSuccessStatusCode)
                {
                    Logger.Error("Snapshot verification failed ({Status}):\n{Body}", verifyResponse.StatusCode, verifyBody);
                    throw new InvalidOperationException(
                        $"Snapshot verification failed: {verifyResponse.StatusCode}\n{verifyBody}");
                }

                Logger.Information("Snapshot verification passed");
            }

            // 10. Run crash test (conditional, Linux only)
            if (scenario.RunCrashTest && !scenario.IsNoop && !scenario.IsWindows)
            {
                LogSection("Running crash test");
                crashTestContainerId = await RunCrashTestAsync(
                    client, imageTag, networkName,
                    environment.ToHostPath(logsDir),
                    environment.ToHostPath(dumpsDir));
            }
        }
        finally
        {
            // Dump container logs before cleanup for debugging
            LogSection("Container logs");
            await DumpContainerLogsAsync(client, "smoke-test", smokeTestContainerId);
            await DumpContainerLogsAsync(client, "crash-test", crashTestContainerId);
            await DumpContainerLogsAsync(client, "test-agent", testAgentContainerId);

            // Cleanup: remove containers, disconnect ourselves from the network, then remove it
            LogSection("Cleanup");
            await CleanupContainerAsync(client, smokeTestContainerId);
            await CleanupContainerAsync(client, crashTestContainerId);
            await CleanupContainerAsync(client, testAgentContainerId);
            await DisconnectFromNetworkAsync(client, networkName, buildContainerId);
            await CleanupNetworkAsync(client, networkName);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Container parameter builders
    // ──────────────────────────────────────────────────────────────

    static CreateContainerParameters BuildTestAgentContainerParams(
        string networkName,
        string snapshotsDir,
        string debugSnapshotsDir)
    {
        return new CreateContainerParameters
        {
            Image = TestAgentImage,
            Env = new List<string>
            {
                "ENABLED_CHECKS=trace_count_header,meta_tracer_version_header,trace_content_length",
                "SNAPSHOT_CI=1",
                $"SNAPSHOT_IGNORED_ATTRS={SnapshotIgnoredAttrs}",
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["8126/tcp"] = default,
            },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["8126/tcp"] = new List<PortBinding> { new() { HostPort = "0" } },
                },
                Binds = new List<string>
                {
                    $"{snapshotsDir}:/snapshots:ro",
                    $"{debugSnapshotsDir}:/debug_snapshots",
                },
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new EndpointSettings
                    {
                        Aliases = new List<string> { "test-agent" },
                    },
                },
            },
        };
    }

    static CreateContainerParameters BuildSmokeTestAppContainerParams(
        string imageTag,
        string networkName,
        string logsDir,
        string dumpsDir)
    {
        return new CreateContainerParameters
        {
            Image = imageTag,
            Env = new List<string>
            {
                "DD_TRACE_AGENT_URL=http://test-agent:8126",
                "DD_PROFILING_ENABLED=1",
                $"dockerTag={imageTag}",
            },
            HostConfig = new HostConfig
            {
                Init = true,
                Binds = new List<string>
                {
                    $"{logsDir}:/var/log/datadog/dotnet",
                    $"{dumpsDir}:/dumps",
                },
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new EndpointSettings(),
                },
            },
        };
    }

    static CreateContainerParameters BuildCrashTestContainerParams(
        string imageTag,
        string networkName,
        string logsDir,
        string dumpsDir)
    {
        return new CreateContainerParameters
        {
            Image = imageTag,
            Env = new List<string>
            {
                "DD_TRACE_AGENT_URL=http://test-agent:8126",
                "DD_PROFILING_ENABLED=0",
                "CRASH_APP_ON_STARTUP=1",
                "DD_CRASHTRACKING_INTERNAL_LOG_TO_CONSOLE=1",
                "COMPlus_DbgEnableMiniDump=0",
                $"dockerTag={imageTag}",
            },
            HostConfig = new HostConfig
            {
                Init = true,
                Binds = new List<string>
                {
                    $"{logsDir}:/var/log/datadog/dotnet",
                    $"{dumpsDir}:/dumps",
                },
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new EndpointSettings(),
                },
            },
        };
    }

    static CreateContainerParameters BuildWindowsTestAgentContainerParams(
        string networkName,
        string snapshotsDir,
        string debugSnapshotsDir)
    {
        return new CreateContainerParameters
        {
            Image = WindowsTestAgentImage,
            Env = new List<string>
            {
                "ENABLED_CHECKS=trace_count_header,meta_tracer_version_header,trace_content_length",
                "SNAPSHOT_CI=1",
                $"SNAPSHOT_IGNORED_ATTRS={SnapshotIgnoredAttrs}",
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["8126/tcp"] = default,
            },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["8126/tcp"] = new List<PortBinding> { new() { HostPort = "0" } },
                },
                Binds = new List<string>
                {
                    $"{snapshotsDir}:c:/snapshots:ro",
                    $"{debugSnapshotsDir}:c:/debug_snapshots",
                },
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new EndpointSettings
                    {
                        Aliases = new List<string> { "test-agent" },
                    },
                },
            },
        };
    }

    static CreateContainerParameters BuildWindowsSmokeTestAppContainerParams(
        string imageTag,
        string networkName,
        string logsDir)
    {
        return new CreateContainerParameters
        {
            Image = imageTag,
            Env = new List<string>
            {
                "DD_TRACE_AGENT_URL=http://test-agent:8126",
                "DD_PROFILING_ENABLED=1",
                $"dockerTag={imageTag}",
            },
            HostConfig = new HostConfig
            {
                // No Init on Windows (Linux-only feature)
                Binds = new List<string>
                {
                    $"{logsDir}:c:/logs",
                },
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new EndpointSettings(),
                },
            },
        };
    }

    // ──────────────────────────────────────────────────────────────
    // Network connectivity
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures whether we're running inside a container (CI) or on the host (local dev).
    /// When in a container, <see cref="BuildContainerId"/> is set and <see cref="ToHostPath"/>
    /// translates container-local paths to Docker-host paths for use in bind mounts.
    /// </summary>
    record DockerEnvironment(string BuildContainerId, Func<string, string> ToHostPath);

    /// <summary>
    /// Detects whether we're running inside a Docker container. If so, joins the smoke-test
    /// network (so we can reach containers by DNS alias) and inspects our own mounts to build
    /// a path translator for bind mounts. On the host, returns an identity translator.
    /// Must be called before creating any containers that need bind mounts.
    /// </summary>
    static async Task<DockerEnvironment> DetectEnvironmentAsync(DockerClient client, string networkName)
    {
        try
        {
            var buildContainerId = Environment.MachineName;
            await client.Networks.ConnectNetworkAsync(
                networkName,
                new NetworkConnectParameters { Container = buildContainerId });
            Logger.Information("Joined network {Network} as {Id} — running in container", networkName, buildContainerId);

            // Inspect our own container to discover mount mappings (container path → host path)
            var inspection = await client.Containers.InspectContainerAsync(buildContainerId);
            var mounts = inspection.Mounts
                .Where(m => !string.IsNullOrEmpty(m.Destination) && !string.IsNullOrEmpty(m.Source))
                .OrderByDescending(m => m.Destination.Length) // longest prefix first
                .ToList();

            foreach (var m in mounts)
            {
                Logger.Debug("Mount: {Source} -> {Destination} (Type: {Type})", m.Source, m.Destination, m.Type);
            }

            string ToHostPath(string containerPath)
            {
                foreach (var mount in mounts)
                {
                    var dest = mount.Destination.TrimEnd('/');
                    if (containerPath == dest || containerPath.StartsWith(dest + "/", StringComparison.Ordinal))
                    {
                        var relativePart = containerPath.Substring(dest.Length);
                        var hostPath = mount.Source.TrimEnd('/') + relativePart;
                        Logger.Debug("Translated path {Container} -> {Host}", containerPath, hostPath);
                        return hostPath;
                    }
                }

                Logger.Warning("No mount found for path {Path}, using as-is", containerPath);
                return containerPath;
            }

            return new DockerEnvironment(buildContainerId, ToHostPath);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Could not join network {Network} (likely running on host)", networkName);
            return new DockerEnvironment(null, path => path);
        }
    }

    /// <summary>
    /// Returns the base URL to reach the test-agent HTTP API.
    /// In a container we use DNS; on the host we use the mapped port.
    /// </summary>
    static async Task<string> GetTestAgentUrlAsync(
        DockerClient client, DockerEnvironment environment, string testAgentContainerId)
    {
        if (environment.BuildContainerId is not null)
        {
            return "http://test-agent:8126";
        }

        var inspection = await client.Containers.InspectContainerAsync(testAgentContainerId);
        var hostPort = inspection.NetworkSettings.Ports["8126/tcp"][0].HostPort;
        Logger.Information("Test agent listening on host port {Port}", hostPort);
        return $"http://localhost:{hostPort}";
    }

    static async Task DisconnectFromNetworkAsync(DockerClient client, string networkName, string containerId)
    {
        if (containerId is null)
        {
            return;
        }

        try
        {
            await client.Networks.DisconnectNetworkAsync(
                networkName,
                new NetworkDisconnectParameters { Container = containerId });
            Logger.Debug("Disconnected {Id} from network {Network}", containerId, networkName);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to disconnect {Id} from network {Network}", containerId, networkName);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Container lifecycle helpers
    // ──────────────────────────────────────────────────────────────

    static async Task PullImageAsync(DockerClient client, string image)
    {
        var parts = image.Split(':');
        var repo = parts[0];
        var tag = parts.Length > 1 ? parts[1] : "latest";

        await RetryAsync(
            $"Pull image {image}",
            async () =>
            {
                Logger.Information("Pulling image {Image}...", image);
                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = repo, Tag = tag },
                    authConfig: null,
                    progress: new Progress<JSONMessage>(msg =>
                    {
                        if (!string.IsNullOrEmpty(msg.Status))
                        {
                            var progress = string.IsNullOrEmpty(msg.ProgressMessage) ? "" : " " + msg.ProgressMessage;
                            if (string.IsNullOrEmpty(msg.ID))
                                Logger.Debug("[Pull] {Status}{Progress}", msg.Status, progress);
                            else
                                Logger.Debug("[Pull] [{Id}] {Status}{Progress}", msg.ID, msg.Status, progress);
                        }
                    }));
                Logger.Information("Pulled image {Image}", image);
            },
            RetryDelays);
    }

    /// <summary>
    /// Creates and starts a container with retry logic. On failure, any partially-created
    /// container is cleaned up before the next attempt.
    /// </summary>
    static async Task<string> CreateAndStartContainerWithRetryAsync(
        DockerClient client,
        string description,
        CreateContainerParameters createParams,
        CancellationToken ct = default)
    {
        string containerId = null;
        return await RetryAsync(
            $"Create/start {description}",
            async () =>
            {
                // Clean up partially-created container from a previous attempt
                if (containerId is not null)
                {
                    await CleanupContainerAsync(client, containerId);
                    containerId = null;
                }

                Logger.Information("Creating {Description} container...", description);
                var response = await client.Containers.CreateContainerAsync(createParams, ct);
                containerId = response.ID;

                Logger.Information("Starting {Description} container {Id}...", description, containerId[..12]);
                await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);

                return containerId;
            },
            RetryDelays,
            ct);
    }

    static async Task WaitForTestAgentAsync(HttpClient httpClient)
    {
        Logger.Information("Waiting for test agent to be ready...");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeoutCts.Token;

        try
        {
            while (true)
            {
                try
                {
                    var response = await httpClient.GetAsync("/", ct);
                    Logger.Information("Test agent is ready (status: {Status})", response.StatusCode);
                    return;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    await Task.Delay(500, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Test agent did not become ready within 30 seconds");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Session data & snapshot verification
    // ──────────────────────────────────────────────────────────────

    static async Task DumpSessionDataAsync(
        HttpClient httpClient,
        string sessionToken,
        string snapshotPrefix,
        AbsolutePath snapshotsDir)
    {
        Logger.Information("Dumping session data...");

        var endpoints = new (string Name, string Path)[]
        {
            ("traces", $"/test/session/traces?test_session_token={sessionToken}"),
            ("stats", $"/test/session/stats?test_session_token={sessionToken}"),
            ("requests", $"/test/session/requests?test_session_token={sessionToken}"),
        };

        foreach (var (name, path) in endpoints)
        {
            Logger.Information("Dumping {Name}...", name);

            var content = await RetryAsync(
                $"Dump {name}",
                async () =>
                {
                    var response = await httpClient.GetAsync(path);
                    return await response.Content.ReadAsStringAsync();
                },
                RetryDelays);

            var outputFile = snapshotsDir / $"{snapshotPrefix}_{name}.json";
            await File.WriteAllTextAsync(outputFile, content);
            Logger.Information("Saved {Name} to {File}", name, outputFile);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Crash test
    // ──────────────────────────────────────────────────────────────

    static async Task<string> RunCrashTestAsync(
        DockerClient client,
        string imageTag,
        string networkName,
        string logsDir,
        string dumpsDir)
    {
        using var crashCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var ct = crashCts.Token;

        var containerId = await CreateAndStartContainerWithRetryAsync(
            client, "crash-test", BuildCrashTestContainerParams(imageTag, networkName, logsDir, dumpsDir), ct);

        // Wait for the container to exit (non-zero exit is expected)
        var waitResponse = await client.Containers.WaitContainerAsync(containerId, ct);
        Logger.Information("Crash test container exited with code {Code}", waitResponse.StatusCode);

        // Read container logs to check for the crash detection message
        var logs = await ReadContainerLogsAsync(client, containerId, ct);

        const string expectedMessage = "The crash may have been caused by automatic instrumentation";
        if (logs.Contains(expectedMessage))
        {
            Logger.Information("Crash test passed: found evidence of crash detection");
        }
        else
        {
            Logger.Error("Crash test failed: did not find expected message in logs:\n{Logs}", logs);
            throw new InvalidOperationException(
                $"Crash test failed: did not find '{expectedMessage}' in container logs");
        }

        return containerId;
    }

    static async Task<string> ReadContainerLogsAsync(DockerClient client, string containerId, CancellationToken ct)
    {
        var logStream = await client.Containers.GetContainerLogsAsync(
            containerId,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true },
            ct);

        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        await logStream.CopyOutputToAsync(Stream.Null, stdout, stderr, ct);

        stdout.Position = 0;
        stderr.Position = 0;

        var sb = new StringBuilder();
        sb.Append(new StreamReader(stdout).ReadToEnd());
        sb.Append(new StreamReader(stderr).ReadToEnd());
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────────────────────

    static async Task DumpContainerLogsAsync(DockerClient client, string name, string containerId)
    {
        if (containerId is null)
        {
            return;
        }

        try
        {
            var logs = await ReadContainerLogsAsync(client, containerId, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(logs))
            {
                Logger.Information("=== Logs from {Name} ({Id}) ===\n{Logs}", name, containerId[..12], logs);
            }
            else
            {
                Logger.Information("=== No logs from {Name} ({Id}) ===", name, containerId[..12]);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to read logs from {Name} ({Id})", name, containerId[..12]);
        }
    }

    static async Task CleanupContainerAsync(DockerClient client, string containerId)
    {
        if (containerId is null)
        {
            return;
        }

        try
        {
            await client.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = true });
            Logger.Debug("Removed container {Id}", containerId[..12]);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to remove container {Id}", containerId[..12]);
        }
    }

    static async Task CleanupNetworkAsync(DockerClient client, string networkName)
    {
        try
        {
            await client.Networks.DeleteNetworkAsync(networkName);
            Logger.Debug("Removed network {Network}", networkName);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to remove network {Network}", networkName);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Retry helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Retries an async action on failure. The number of retries equals <paramref name="retryDelays"/>.Length,
    /// so total attempts = retryDelays.Length + 1. Cancellation via <paramref name="ct"/> is never retried.
    /// </summary>
    static async Task RetryAsync(string operation, Func<Task> action, TimeSpan[] retryDelays, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < retryDelays.Length && !ct.IsCancellationRequested)
            {
                Logger.Warning(ex, "{Operation} failed (attempt {Attempt}/{Total}), retrying in {Delay}s...",
                    operation, attempt + 1, retryDelays.Length + 1, retryDelays[attempt].TotalSeconds);
                await Task.Delay(retryDelays[attempt], ct);
            }
        }
    }

    /// <summary>
    /// Retries an async function on failure, returning its result on success.
    /// </summary>
    static async Task<T> RetryAsync<T>(string operation, Func<Task<T>> action, TimeSpan[] retryDelays, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < retryDelays.Length && !ct.IsCancellationRequested)
            {
                Logger.Warning(ex, "{Operation} failed (attempt {Attempt}/{Total}), retrying in {Delay}s...",
                    operation, attempt + 1, retryDelays.Length + 1, retryDelays[attempt].TotalSeconds);
                await Task.Delay(retryDelays[attempt], ct);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Image build internals
    // ──────────────────────────────────────────────────────────────

    static async Task BuildImageFromDockerfileAsync(
        AbsolutePath contextDir,
        string dockerfilePath,
        string tag,
        Dictionary<string, string> buildArgs,
        AbsolutePath artifactsDir,
        string target = null)
    {
        // Build the context tar once — MemoryStream is re-seekable for retries
        using var contextStream = CreateBuildContextTar(contextDir, dockerfilePath, artifactsDir);

        var buildParams = new ImageBuildParameters
        {
            Dockerfile = dockerfilePath,
            Tags = new List<string> { tag },
            BuildArgs = buildArgs,
            Target = target,
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
                        var progress = string.IsNullOrEmpty(msg.ProgressMessage) ? "" : " " + msg.ProgressMessage;
                        if (string.IsNullOrEmpty(msg.ID))
                            Logger.Debug("{Status}{Progress}", msg.Status, progress);
                        else
                            Logger.Debug("[{Id}] {Status}{Progress}", msg.ID, msg.Status, progress);
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
                    progress: progress);

                if (lastError is not null)
                {
                    throw new InvalidOperationException($"Docker build failed: {lastError}");
                }

                Logger.Information("Successfully built image {Tag}", tag);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = RetryDelays[attempt - 1];
                Logger.Warning(ex, "Docker build attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s...", attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
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

    /// <summary>
    /// Renames the first file matching <paramref name="searchPattern"/> in <paramref name="directory"/>
    /// to <paramref name="targetName"/>. Used to normalize artifact file names before Docker builds.
    /// </summary>
    static void RenameArtifact(AbsolutePath directory, string searchPattern, string targetName)
    {
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        var targetPath = Path.Combine(directory, targetName);
        if (File.Exists(targetPath))
        {
            Logger.Debug("Artifact {Target} already exists, skipping rename", targetPath);
            return;
        }

        var files = Directory.GetFiles(directory, searchPattern);
        if (files.Length == 0)
        {
            Logger.Warning("No files matching {Pattern} found in {Dir}", searchPattern, directory);
            return;
        }

        Logger.Information("Renaming {Source} -> {Target}", Path.GetFileName(files[0]), targetName);
        File.Move(files[0], targetPath);
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

    static void LogSection(string title)
    {
        Logger.Information("────────────────────────────────────────────────────────────");
        Logger.Information("{Title}", title);
        Logger.Information("────────────────────────────────────────────────────────────");
    }
}
