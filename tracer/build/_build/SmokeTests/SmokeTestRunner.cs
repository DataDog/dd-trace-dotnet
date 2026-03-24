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
using static SmokeTests.Helpers;

namespace SmokeTests;

public static partial class SmokeTestRunner
{
    const string TestAgentAlias = "test-agent";

    // ──────────────────────────────────────────────────────────────
    // Smoke test orchestration
    // ──────────────────────────────────────────────────────────────

    public static async Task RunSmokeTestAsync(
        SmokeTestCategory category,
        string scenarioName,
        AbsolutePath tracerDir,
        AbsolutePath artifactsDir,
        AbsolutePath buildDataDir,
        string toolVersion,
        string dotnetSdkVersion)
    {
        var scenario = SmokeTestScenarios.GetScenario(category, scenarioName);

        var imageTags = await Builder.BuildImageAsync(scenario, tracerDir, artifactsDir, toolVersion, dotnetSdkVersion);

        // Ensure output directories exist
        // debugSnapshotsDir: mounted as /debug_snapshots in the test-agent container,
        // also where we write dumped traces/stats/requests from the host
        var debugSnapshotsDir = buildDataDir / "snapshots";
        var logsDir = buildDataDir / "logs";
        var dumpsDir = buildDataDir / "dumps";
        EnsureCleanDirectory(debugSnapshotsDir);
        EnsureCleanDirectory(logsDir);
        EnsureCleanDirectory(dumpsDir);

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

        foreach (var imageTag in imageTags)
        {
            await RunSmokeTestAsync(scenario, tracerDir, imageTag, debugSnapshotsDir, logsDir, dumpsDir);
        }
    }

    static async Task RunSmokeTestAsync(
        SmokeTestScenario scenario,
        AbsolutePath tracerDir,
        string imageTag,
        AbsolutePath debugSnapshotsDir,
        AbsolutePath logsDir,
        AbsolutePath dumpsDir)
    {
        LogSection($"Running smoke test: {imageTag}");

        var networkName = $"smoke-test-{Guid.NewGuid():N}";
        string testAgentContainerId = null;
        string smokeTestContainerId = null;
        string crashTestContainerId = null;
        DockerService.DockerEnvironment environment = null;

        try
        {
            // 1. Create a dedicated Docker network
            await DockerService.CreateNetwork(networkName);

            // 2. Detect environment (container vs host) for path translation & networking
            //    Must happen before creating containers, because bind mount paths need
            //    translating when running inside a container (Docker-in-Docker).
            environment = await DockerService.DetectEnvironmentAsync(networkName);

            // 3. Pull/build + start test-agent container
            LogSection("Starting test agent");
            var sourceSnapshotsDir = tracerDir / "build" / "smoke_test_snapshots";
            var testAgentImage = await Builder.BuildTestAgentImageAsync(scenario, tracerDir);
            testAgentContainerId = await DockerService.CreateAndStartContainerWithRetryAsync(
                TestAgentAlias, BuildTestAgentContainerParams(
                    testAgentImage: testAgentImage,
                    networkName: networkName,
                    snapshotsDir: environment.ToHostPath(sourceSnapshotsDir),
                    debugSnapshotsDir: environment.ToHostPath(debugSnapshotsDir),
                    snapshotIgnoredAttrs: scenario.SnapshotIgnoredAttrs,
                    isWindowsScenario: scenario.IsWindows));

            // 4. Determine how to reach the test-agent's HTTP API
            var testAgentBaseUrl = await DockerService.GetContainerUrlAsync(environment, testAgentContainerId, TestAgentAlias, 8126);
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
            smokeTestContainerId = await DockerService.CreateAndStartContainerWithRetryAsync(
                "smoke-test", BuildSmokeTestAppContainerParams(
                    imageTag: imageTag,
                    networkName: networkName,
                    logsDir: environment.ToHostPath(logsDir),
                    dumpsDir: environment.ToHostPath(dumpsDir),
                    isWindowsScenario: scenario.IsWindows,
                    scenario.GetEnvironment(isCrashTest: false)));

            if (await DockerService.WaitForContainerAsync("Smoke test", smokeTestContainerId) is var statusCode and not 0)
            {
                throw new InvalidOperationException($"Smoke test container exited with code {statusCode}");
            }

            // 8. Dump traces/stats/requests from test-agent
            LogSection("Verifying results");
            await DumpSessionDataAsync(httpClient, sessionToken, "smoke_test", debugSnapshotsDir);

            // 9. Verify snapshots
            await VerifySnapshots(scenario, httpClient, sessionToken);

            // 10. Run crash test
            if (scenario.RunCrashTest)
            {
                crashTestContainerId = await RunCrashTestAsync(
                    imageTag, networkName,
                    environment.ToHostPath(logsDir),
                    environment.ToHostPath(dumpsDir),
                    scenario.GetEnvironment(isCrashTest: true));
            }
        }
        finally
        {
            // Dump container logs before cleanup for debugging
            LogSection("Container logs");
            await DumpContainerLogsAsync("smoke-test", smokeTestContainerId);
            await DumpContainerLogsAsync("crash-test", crashTestContainerId);
            await DumpContainerLogsAsync(TestAgentAlias, testAgentContainerId);

            // Cleanup: remove containers, disconnect ourselves from the network, then remove it
            LogSection("Cleanup");
            // The order of containerIds is important here, pass them in "reverse" dependency order
            await DockerService.Cleanup(environment, networkName, smokeTestContainerId, crashTestContainerId, testAgentContainerId);
        }
    }

    static CreateContainerParameters BuildTestAgentContainerParams(
        string testAgentImage,
        string networkName,
        string snapshotsDir,
        string debugSnapshotsDir,
        string snapshotIgnoredAttrs,
        bool isWindowsScenario)
    {
        return new CreateContainerParameters
        {
            Image = testAgentImage,
            Env = new List<string>
            {
                "ENABLED_CHECKS=trace_count_header,meta_tracer_version_header,trace_content_length",
                "SNAPSHOT_CI=1",
                $"SNAPSHOT_IGNORED_ATTRS={snapshotIgnoredAttrs}",
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
                Binds = isWindowsScenario
                    ? new List<string>
                    {
                        $"{snapshotsDir}:c:/snapshots:ro",
                        $"{debugSnapshotsDir}:c:/debug_snapshots",
                    }
                    : new List<string>
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
                        Aliases = new List<string> {TestAgentAlias},
                    },
                },
            },
        };
    }

    static CreateContainerParameters BuildSmokeTestAppContainerParams(
        string imageTag,
        string networkName,
        string logsDir,
        string dumpsDir,
        bool isWindowsScenario,
        Dictionary<string, string> environment)
    {
        var env = new List<string>
        {
            $"DD_TRACE_AGENT_URL=http://{TestAgentAlias}:8126",
            $"dockerTag={imageTag}",
        };
        env.AddRange(environment.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        return new CreateContainerParameters
        {
            Image = imageTag,
            Env = env,
            HostConfig = new HostConfig
            {
                // No Init or SYS_PTRACE on Windows (Linux-only features)
                Init = !isWindowsScenario,
                CapAdd = isWindowsScenario ? null : new List<string> { "SYS_PTRACE" },
                Binds = isWindowsScenario
                    ? new List<string>
                    {
                        $"{logsDir}:c:/logs",
                    }
                    : new List<string>
                    {
                        $"{logsDir}:/var/log/datadog/dotnet",
                        $"{dumpsDir}:/dumps",
                    }
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkName] = new(),
                },
            },
        };
    }

    // ──────────────────────────────────────────────────────────────
    // Test agent helpers
    // ──────────────────────────────────────────────────────────────

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

    static async Task VerifySnapshots(SmokeTestScenario scenario, HttpClient httpClient, string sessionToken)
    {
        if (scenario.IsNoop)
        {
            Logger.Information("No-op scenario, skipping verification");
            return;
        }

        Logger.Information("Verifying snapshot {File}...", scenario.SnapshotFile);

        // Windows containers mount snapshots at c:/snapshots, Linux at /snapshots
        var snapshotPathPrefix = scenario.IsWindows ? "c:/snapshots" : "/snapshots";

        // Retry the HTTP call itself (transport errors), but not a successful response indicating a real snapshot mismatch
        var verifyResponse = await RetryAsync(
            $"Verify snapshot {scenario.SnapshotFile}",
            () => httpClient.GetAsync(
                $"/test/session/snapshot?test_session_token={sessionToken}&file={snapshotPathPrefix}/{scenario.SnapshotFile}"),
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

    static async Task<string> RunCrashTestAsync(
        string imageTag,
        string networkName,
        string logsDir,
        string dumpsDir,
        Dictionary<string, string> environmentVariables)
    {
        LogSection("Running crash test");
        using var crashCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var ct = crashCts.Token;

        var containerParams = BuildSmokeTestAppContainerParams(
            imageTag: imageTag,
            networkName: networkName,
            logsDir: logsDir,
            dumpsDir: dumpsDir,
            isWindowsScenario: false, // We don't yet support windows crash-tests
            environmentVariables);
        var containerId = await DockerService.CreateAndStartContainerWithRetryAsync(
            "crash-test", containerParams, ct);

        // Wait for the container to exit (non-zero exit is expected)
        var statusCode = await DockerService.WaitForContainerAsync("Crash test", containerId, ct);
        if (statusCode == 0)
        {
            throw new InvalidOperationException($"Crash test container unexpectedly exited with code {statusCode}");
        }

        // Read container logs to check for the crash detection message
        var logs = await DockerService.ReadContainerLogsAsync(containerId, ct);

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


    static async Task DumpContainerLogsAsync(string name, string containerId)
    {
        if (containerId is null)
        {
            return;
        }

        try
        {
            var logs = await DockerService.ReadContainerLogsAsync(containerId, CancellationToken.None);
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
}
