using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Nuke.Common;
using Nuke.Common.IO;
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
    readonly SmokeTests.SmokeTestCategory? SmokeTestCategory;
    [Parameter("The specific scenario for the given category to run", ValueProviderMember = typeof())]
    readonly string SmokeTestScenario;

    Target RunArtifactSmokeTests => _ => _
        .Description("Runs the artifact snapshot/smoke tests ")
        .Unlisted()
        .Requires(() => SmokeTestCategory)
        .Requires(() => SmokeTestScenario)
        .Triggers(CheckSmokeTestsForErrors) // TODO: inline this in this target
        .Executes(async () =>
        {
            var debugSnapshotsDir = BuildDataDirectory / "snapshots";
            var logDir = TestLogsDirectory / "smoke_tests";

            EnsureCleanDirectory(logDir);
            EnsureExistingDirectory(TestDumpsDirectory);
            EnsureCleanDirectory(debugSnapshotsDir);

            // TODO: copy the artifacts to the correct place based on the scenario
            // TODO: ensure prerequisites? e.g. artifacts are in correct place?

            var category = SmokeTestCategory!.Value; // Required so not-null
            var smokeTest = SmokeTests.SmokeTestBuilder.GetScenario(category, SmokeTestScenario);

           // build the smoke test container - we could/should consider defining the dockerfiles dynamically here,
           // but for now just use the existing files
           var jobId = Guid.NewGuid();
           Logger.Information("Test session ID={JobId}", jobId);

           Logger.Information("👷‍♂️ Building test image for {SmokeTestName}", smokeTest.ShortName);
           SmokeTests.SmokeTestBuilder.BuildImage(category, smokeTest, TracerDirectory);

            // start the test agent first
            Logger.Information("▶ Starting test agent test image for {SmokeTestName}", smokeTest.ShortName);

            var (network, testAgentHostName, testAgent) = await StartTestAgent(jobId);

            var httpClient = new HttpClient
            {
                BaseAddress = new UriBuilder("http", testAgent.Hostname, testAgent.GetMappedPublicPort(8126)).Uri,
            };

            Logger.Information("✍ Starting snapshot session");
            var result = await httpClient.GetAsync($"/test/session/start?test_session_token={jobId}");
            Logger.Debug("Snapshots response: {Response}", await result.Content.ReadAsStringAsync());
            result.EnsureSuccessStatusCode();

            Logger.Information("🧪 Starting test app");
            await RunTestAppAndWaitForExit(smokeTest, network, testAgentHostName, jobId, logDir);

            Logger.Information("🥟 Dumping stats and traces to {SnapshotDirectory}", debugSnapshotsDir);
            var traces = await httpClient.GetStringAsync($"/test/session/traces?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_traces.json", traces);

            var stats = await httpClient.GetStringAsync($"/test/session/stats?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_stats.json", stats);

            var requests = await httpClient.GetStringAsync($"/test/session/requests?test_session_token={jobId}");
            File.WriteAllText(debugSnapshotsDir / "smoke_test_requests.json", requests);

            Logger.Information("💾 Comparing with saved snapshots...");

            var snapshotFile = smokeTest.PublishFramework == TargetFramework.NETCOREAPP2_1 ? "smoke_test_snapshots_2_1" : "smoke_test_snapshots";
            result = await httpClient.GetAsync($"test/session/snapshot?test_session_token={jobId}&file=/snapshots/{snapshotFile}");
            if (result.IsSuccessStatusCode)
            {
                Logger.Information("✅ Snapshots verified");
            }
            else
            {
                var body = await result.Content.ReadAsStringAsync();
                Logger.Error("❌ Snapshot verification failed: {Result}", body);
                result.EnsureSuccessStatusCode();
            }

            return;

            async Task<(INetwork network, string testAgentHostName, IContainer testAgent)> StartTestAgent(Guid jobId)
            {
                var network1 = new NetworkBuilder()
                              .WithName($"smoke-tests-{jobId:N}")
                              .WithCleanUp(true)
                              .WithLogger(SerilogWrapperLogger.Instance)
                              .Build();

                var sourceSnapshotsDir = TracerDirectory / "build" / "smoke_test_snapshots";
                var s = "test-agent";
                var container = new ContainerBuilder()
                               .WithImage("ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest")
                               .WithName($"test-agent-{jobId:N}")
                               .WithPortBinding(8126, true)
                               .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8126))
                               .WithResourceMapping(new DirectoryInfo(sourceSnapshotsDir), "/snapshots")
                               .WithEnvironment("ENABLED_CHECKS", "trace_count_header,meta_tracer_version_header,trace_content_length")
                               .WithEnvironment("SNAPSHOT_CI", "1")
                                // network.client.ip and http.client_ip differs between docker compose v1 and v2
                                // - once we're migrated to v2 completely we can remove it from here
                                // api-security attrs are unfortunately ignored because gzip compression generates
                                // different bytes per platform windows/linux
                               .WithEnvironment("SNAPSHOT_IGNORED_ATTRS", "span_id,trace_id,parent_id,duration,start,metrics.system.pid,meta.runtime-id,meta.network.client.ip,meta.http.client_ip,metrics.process_id,meta._dd.p.dm,meta._dd.p.tid,meta._dd.parent_id,meta._dd.appsec.s.req.params,meta._dd.appsec.s.res.body,meta._dd.appsec.s.req.headers,meta._dd.appsec.s.res.headers")
                               .WithLogger(SerilogWrapperLogger.Instance)
                               .WithNetwork(network1)
                               .WithNetworkAliases(s)
                               .WithCleanUp(true)
                               .Build();

                await network1.CreateAsync().ConfigureAwait(false);
                await container.StartAsync().ConfigureAwait(false);
                return (network1, s, container);
            }

            async Task RunTestAppAndWaitForExit(
                SmokeTests.SmokeTestScenario image,
                INetwork network,
                string testAgentHostName,
                Guid jobId,
                AbsolutePath logDir)
            {
                // TODO: make sure that the container have enough rights to write in this folder
                // sudo chmod -R 777 artifacts/build_data/ || true
                var mappedLogDir = image.IsLinuxContainer ? "/var/log/datadog/dotnet" : "c:/logs";
                var mappedDumpDir = image.IsLinuxContainer ? "/dumps" : "c:/dumps";
                var testapp = new ContainerBuilder()
                             .WithImage(image.DockerTag)
                             .WithName($"test-app-{jobId:N}")
                             .WithEnvironment("dockerTag", image.RuntimeTag)
                             .WithEnvironment("PROFILER_IS_NOT_REQUIRED", image.IsNoop ? "true" : "false")
                             .WithEnvironment("DD_TRACE_AGENT_URL", $"http://{testAgentHostName}:8126")
                             .WithNetwork(network)
                             .WithBindMount(logDir, mappedLogDir)
                             .WithBindMount(TestDumpsDirectory, mappedDumpDir)
                             .WithLogger(SerilogWrapperLogger.Instance)
                             .WithCleanUp(true)
                             .Build();


                // Session has started, run the test app
                await testapp.StartAsync().ConfigureAwait(false);

                // wait for testapp to be finished
                // we can't use anything test containers provides because it doesn't update the state
                var deadline = DateTime.UtcNow.AddMinutes(2);
                var testAppId = testapp.Id;
                Logger.Information("Waiting for test app to finish....");
                var exited = false;
                while(DateTime.UtcNow < deadline)
                {
                    var containerOutput = DockerTasks.DockerPs(c => c
                                                                   .SetFilter($"id={testAppId}")
                                                                   .SetQuiet(true));
                    if (containerOutput.Any(x => x.Text == testAppId))
                    {
                        Console.Write(".");
                        await Task.Delay(1000);
                    }

                    exited = true;
                    break;
                }

                if (!exited)
                {
                    throw new Exception("Test app failed to stop gracefully after 2 minutes");
                }

                var exitCode = await testapp.GetExitCodeAsync().ConfigureAwait(false);
                if (exitCode != 0)
                {
                    // TODO: Log more stuff if this happens
                    throw new Exception($"Test app returned a non-zero exit code: {exitCode}");
                }
            }
        });
}
