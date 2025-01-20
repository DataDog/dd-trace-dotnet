using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Nuke.Common;
using Nuke.Common.Tools.Docker;
using NukeExtensions;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Parameter("The category of the smoke test to run")]
    readonly string SmokeTestCategory;
    [Parameter("The specific scenario for the given catefory to run")]
    readonly bool SmokeTestScenario;

    Target RunArtifactSmokeTests => _ => _
        .Description("Runs the artifact snapshot/smoke tests ")
        .Unlisted()
        // .Requires(() => SmokeTestCategory)
        // .Requires(() => SmokeTestScenario)
        .Executes(async () =>
        {
            const string dotNetSdkShortVersion = "9.0.100";
            var debugSnapshotsDir = BuildDataDirectory / "snapshots";

            EnsureExistingDirectory(TestLogsDirectory);
            EnsureExistingDirectory(TestDumpsDirectory);
            EnsureExistingDirectory(debugSnapshotsDir);

            // TODO: ensure prerequisites? e.g. artifacts are in correct place?

            var category = SmokeTests.SmokeTestCategory.LinuxX64Installer;
            var smokeTests = SmokeTests.SmokeTestBuilder.GetSmokeTestImagesForCategory(category);
            var smokeTest = smokeTests[0];

           // build the smoke test container - we could/should consider defining the dockerfiles dynamically here,
           // but for now just use the existing files

           Logger.Information("Building test image for {SmokeTestName}", smokeTest.ShortName);
           var tag = SmokeTests.SmokeTestBuilder.BuildImage(category, smokeTest, TracerDirectory);

           var sourceSnapshotsDir = TracerDirectory / "build" / "smoke_test_snapshots";

            if (!IsWin)
            {
                // TODO: make sure that the container have enough rights to write in this folder
                // sudo chmod -R 777 artifacts/build_data/ || true
            }

            // start the test agent first
            var network = new NetworkBuilder()
                         .WithName($"smoke-tests-{Guid.NewGuid():N}")
                         .WithCleanUp(true)
                         .WithLogger(SerilogWrapperLogger.Instance)
                         .Build();

            var testAgent = new ContainerBuilder()
                           .WithImage("ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest")
                           .WithName("test-agent")
                           .WithPortBinding(8126, true)
                           .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8126))
                           .WithBindMount(sourceSnapshotsDir, "/snapshots")
                           .WithEnvironment("ENABLED_CHECKS", "trace_count_header,meta_tracer_version_header,trace_content_length")
                           .WithEnvironment("SNAPSHOT_CI", "1")
                            // network.client.ip and http.client_ip differs between docker compose v1 and v2
                            // - once we're migrated to v2 completely we can remove it from here
                            // api-security attrs are unfortunately ignored because gzip compression generates
                            // different bytes per platform windows/linux
                           .WithEnvironment("SNAPSHOT_IGNORED_ATTRS", "span_id,trace_id,parent_id,duration,start,metrics.system.pid,meta.runtime-id,meta.network.client.ip,meta.http.client_ip,metrics.process_id,meta._dd.p.dm,meta._dd.p.tid,meta._dd.parent_id,meta._dd.appsec.s.req.params,meta._dd.appsec.s.res.body,meta._dd.appsec.s.req.headers,meta._dd.appsec.s.res.headers")
                           .WithLogger(SerilogWrapperLogger.Instance)
                           .WithNetwork(network)
                           .WithCleanUp(true)
                           .Build();

            var testapp = new ContainerBuilder()
                         .WithImage(tag)
                         .WithEnvironment("dockerTag", smokeTest.RuntimeTag)
                         .WithEnvironment("PROFILER_IS_NOT_REQUIRED", smokeTest.IsNoop ? "true" : "false")
                         .WithNetwork(network)
                         .WithLogger(SerilogWrapperLogger.Instance)
                         .WithCleanUp(true)
                         .Build();

            await network.CreateAsync().ConfigureAwait(false);
            await testAgent.StartAsync().ConfigureAwait(false);

            var jobId = Guid.NewGuid();
            Logger.Information("Test session ID={JobId}", jobId);

            var httpClient = new HttpClient
            {
                BaseAddress = new UriBuilder("http", testAgent.Hostname, testAgent.GetMappedPublicPort(8126)).Uri,
            };

            Logger.Information("Starting snapshot session");
            var result = await httpClient.GetAsync($"/test/session/start?test_session_token={jobId}");
            result.EnsureSuccessStatusCode();

            // Session has started, run the test app
            await testapp.StartAsync().ConfigureAwait(false);

            // wait for testapp to be finished
            // Spoiler - this doesn't work because test containers doesn't update anything
            var deadline = DateTime.UtcNow.AddMinutes(2);
            Logger.Information("Waiting for test app to finish....");
            while (testapp.State is not TestcontainersStates.Exited or TestcontainersStates.Dead
                && DateTime.UtcNow < deadline)
            {
                Console.Write(".");
                await Task.Delay(1000);
            }

            if (testapp.State is not TestcontainersStates.Exited)
            {
                throw new Exception("Test app failed to stop gracefully after 2 minutes");
            }

            var exitCode = await testapp.GetExitCodeAsync().ConfigureAwait(false);
            if (exitCode != 0)
            {
                // TODO: Log more stuff if this happens
                throw new Exception($"Test app returned a non-zero exit code: {exitCode}");
            }

            Logger.Information("Dumping stats and traces");
            var traces = await httpClient.GetStringAsync($"/test/session/traces?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_traces.json", traces);

            var stats = await httpClient.GetStringAsync($"/test/session/stats?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_stats.json", stats);

            var requests = await httpClient.GetStringAsync($"/test/session/requests?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_requests.json", requests);

            Logger.Information("Running snapshot check");

            var snapshotFile = Framework == TargetFramework.NETCOREAPP2_1 ? "smoke_test_snapshots" : "smoke_test_snapshots_2_1";
            result = await httpClient.GetAsync($"test/session/snapshot?test_session_token={jobId}&file=/snapshots/{snapshotFile}");
            if (result.IsSuccessStatusCode)
            {
                Logger.Information("Snapshots verified");
            }
            else
            {
                var body = await result.Content.ReadAsStringAsync();
                Logger.Error("Snapshot verification failed: {Result}", body);
                result.EnsureSuccessStatusCode();
            }
        });
}
