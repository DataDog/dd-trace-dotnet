// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

/// <summary>
/// These tests require the Azure Function Core Tools to be installed
/// You can read how to install them here: https://github.com/Azure/azure-functions-core-tools
/// Note that you need a _Different_ version of the tools for V3 and V4
/// And you can't install them side-by-side
/// </summary>
public abstract class AzureFunctionsTests : TestHelper
{
    // The sample waits for the HTTP listener by probing /admin/host/ping, which the host records as one or
    // more "GET /admin/host/ping" spans. They are not part of the traces under test, so they are excluded
    // from the agent's span waits and results (see SuppressReadinessPingSpans).
    protected const string ReadinessPingResource = "GET /admin/host/ping";

    protected AzureFunctionsTests(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides: Path.Combine("test", "test-applications", "azure-functions"), output)
    {
        // Disable Continuous Profiler to avoid error log "Stable Configuration has not been set: the profiler was never started"
        SetEnvironmentVariable("DD_PROFILING_ENABLED", "0");
        SetEnvironmentVariable("DD_PROFILING_MANAGED_ACTIVATION_ENABLED", "0");
        // Ensures we filter out the host span requests etc
        SetEnvironmentVariable("WEBSITE_SKU", "Basic");
        SetEnvironmentVariable("DD_AZURE_APP_SERVICES", "1");
        SetEnvironmentVariable("DD_API_KEY", "NOT_SET"); // required for tracing to activate
        SetEnvironmentVariable("WEBSITE_SITE_NAME", "AzureFunctionsAllTriggers");
        SetEnvironmentVariable("COMPUTERNAME", "IntegrationTestHost");
        // Disable the process integration as func.exe calls out to `where`
        SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Process)}_ENABLED", "0");
        // Add an extra exclude for calls to storage emulator. These aren't necessary in production
        // as they are already covered by the existing excludes
        SetEnvironmentVariable("DD_TRACE_HTTP_CLIENT_EXCLUDED_URL_SUBSTRINGS", ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions + ", devstoreaccount1/azure-webjobs-hosts");
    }

    // Excludes the readiness-probe spans (see ReadinessPingResource) from this agent's span waits and
    // results, so the number of probe attempts never affects WaitForSpansAsync counts.
    protected static void SuppressReadinessPingSpans(MockTracerAgent agent)
        => agent.SpanFilters.Add(s => s.Resource != ReadinessPingResource);

    protected static IList<MockSpan> FilterOutSocketsHttpHandler(IImmutableList<MockSpan> spans)
    {
        IList<MockSpan> filteredSpans = new List<MockSpan>();
        foreach (var span in spans)
        {
            if (span.Tags.TryGetValue("http-client-handler-type", out var val))
            {
                if (val != "System.Net.Http.SocketsHttpHandler")
                {
                    filteredSpans.Add(span);
                }
            }
            else
            {
                filteredSpans.Add(span);
            }
        }

        return filteredSpans;
    }

    protected async Task<ProcessResult> RunAzureFunctionAndWaitForExit(MockTracerAgent agent, Func<Task> seedAsync = null, string framework = null, int expectedExitCode = 0)
    {
        using var helper = await StartAzureFunction(agent, framework);

        if (seedAsync is not null)
        {
            await WaitForFunctionHostHttpEndpointAsync();
            await seedAsync();
        }

        return WaitForProcessResult(helper, expectedExitCode);
    }

    protected async Task RunIsolatedAzureFunctionAsync(MockTracerAgent agent, Func<Task> testAsync, string framework = null)
    {
        // Timer listeners use blob singleton leases. Give every run a unique host ID so a lease
        // left by an earlier test cannot delay this host's timer trigger for up to a minute.
        SetEnvironmentVariable("AzureFunctionsWebHost__hostid", "aftrace" + Guid.NewGuid().ToString("N").Substring(0, 25));

        using var helper = await StartAzureFunction(agent, framework);
        try
        {
            await WaitForFunctionHostHttpEndpointAsync();

            // Keep func.exe and its isolated worker alive until the test has received and asserted all
            // expected data. Shutting down first can discard the host process's final trace batch.
            await testAsync();

            if (helper.Process.HasExited)
            {
                throw new InvalidOperationException("The Azure Functions host exited before the test requested shutdown.");
            }
        }
        finally
        {
            await StopIsolatedAzureFunctionAsync(helper);
        }
    }

    protected async Task<IImmutableList<MockLogsIntake.Log>> WaitForLogsAsync(
        MockLogsIntake logsIntake,
        Func<IImmutableList<MockLogsIntake.Log>, bool> predicate,
        int timeoutInMilliseconds = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            var logs = logsIntake.Logs;
            if (predicate(logs))
            {
                return logs;
            }

            await Task.Delay(250);
        }

        return logsIntake.Logs;
    }

    protected async Task<IImmutableList<MockLogsIntake.Log>> WaitForLogsToStabilizeAsync(
        MockLogsIntake logsIntake,
        int minimumCount,
        int quietPeriodInMilliseconds = 2_000,
        int timeoutInMilliseconds = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);
        var quietPeriod = TimeSpan.FromMilliseconds(quietPeriodInMilliseconds);
        var previousCount = logsIntake.Logs.Count;

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < quietPeriod)
            {
                await Task.Delay(remaining);
                break;
            }

            await Task.Delay(quietPeriod);
            var logs = logsIntake.Logs;
            if (logs.Count >= minimumCount && logs.Count == previousCount)
            {
                return logs;
            }

            previousCount = logs.Count;
        }

        throw new TimeoutException(
            $"Logs did not stabilize at or above {minimumCount} entries within {timeoutInMilliseconds}ms. Last count: {logsIntake.Logs.Count}.");
    }

    protected async Task<ProcessHelper> StartAzureFunction(MockTracerAgent agent, string framework)
    {
        var binFolder = EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion: string.Empty, framework);
        Output.WriteLine("Using binFolder: " + binFolder);
        var process = await ProfilerHelper.StartProcessWithProfiler(
            executable: "func",
            EnvironmentHelper,
            agent,
            "start --verbose",
            aspNetCorePort: 7071, // The default port
            processToProfile: null,
            workingDirectory: binFolder); // points to the sample project

        return new ProcessHelper(process);
    }

    protected async Task StopIsolatedAzureFunctionAsync(ProcessHelper helper)
    {
        var process = helper.Process;
        var childProcessIds = new List<int>();

        if (!process.HasExited)
        {
            childProcessIds.AddRange(ProcessHelper.GetChildrenIds(process.Id));

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await http.PostAsync("http://127.0.0.1:7071/api/shutdown", content: null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Unable to request Azure Functions worker shutdown: {ex.Message}");
            }

            // Core Tools may remain alive after the isolated worker stops. Give it a short grace period,
            // then terminate only this test's process tree. All expected spans and logs were awaited above.
            _ = await Task.WhenAny(helper.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        }

        if (!process.HasExited)
        {
            foreach (var childProcessId in ProcessHelper.GetChildrenIds(process.Id))
            {
                if (!childProcessIds.Contains(childProcessId))
                {
                    childProcessIds.Add(childProcessId);
                }
            }

            try
            {
                process.Kill();
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Unable to stop Azure Functions host process {process.Id}: {ex.Message}");
            }
        }

        for (var i = childProcessIds.Count - 1; i >= 0; i--)
        {
            try
            {
                using var childProcess = Process.GetProcessById(childProcessIds[i]);
                if (!childProcess.HasExited)
                {
                    childProcess.Kill();
                }
            }
            catch (ArgumentException)
            {
                // The child process has already exited.
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Unable to stop Azure Functions child process {childProcessIds[i]}: {ex.Message}");
            }
        }

        if (!process.HasExited)
        {
            process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        }

        helper.Drain((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        if (!string.IsNullOrWhiteSpace(helper.StandardOutput))
        {
            Output.WriteLine($"StandardOutput:{Environment.NewLine}{helper.StandardOutput}");
        }

        if (!string.IsNullOrWhiteSpace(helper.ErrorOutput))
        {
            Output.WriteLine($"StandardError:{Environment.NewLine}{helper.ErrorOutput}");
        }

        Output.WriteLine("ProcessId: " + process.Id);
        if (process.HasExited)
        {
            Output.WriteLine("Exit Code: " + process.ExitCode);
        }
    }

    protected async Task AssertInProcessSpans(IImmutableList<MockSpan> spans)
    {
        // AAS _potentially_ attaches extra tags here, depending on exactly where in the trace the tags are
        // so can't easily validate
        // _ when span.Name == "http.request" => span.IsHttpMessageHandler(),
        // _ when span.Name == "aspnet_core.request" => span.IsAspNetCore(),
        // ValidateIntegrationSpans(spans, expectedServiceName: nameof(InProcessRuntimeV3));

        var settings = VerifyHelper.GetSpanVerifierSettings();
        // v3 runtime and v4 runtime report different versions
        settings.AddSimpleScrubber("aas.environment.runtime: .NET Core", "aas.environment.runtime: .NET");
        settings.AddRegexScrubber(
            new(@"Microsoft.Azure.WebJobs.Extensions, Version=\d.\d.\d.\d"),
            @"Microsoft.Azure.WebJobs.Extensions, Version=0.0.0.0");

        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName($"{nameof(AzureFunctionsTests)}.InProcess")
                          .DisableRequireUniquePrefix();
    }

    protected async Task AssertIsolatedSpans(IImmutableList<MockSpan> spans, string filename = null)
    {
        // AAS _potentially_ attaches extra tags here, depending on exactly where in the trace the tags are
        // so can't easily validate
        // _ when span.Name == "http.request" => span.IsHttpMessageHandler(),
        // _ when span.Name == "aspnet_core.request" => span.IsAspNetCore(),
        // ValidateIntegrationSpans(spans, expectedServiceName: nameof(InProcessRuntimeV3));

        var settings = VerifyHelper.GetSpanVerifierSettings();
        // v3 runtime and v4 runtime report different versions
        settings.AddSimpleScrubber("aas.environment.runtime: .NET Core", "aas.environment.runtime: .NET");
        settings.AddRegexScrubber(
            new(@"Microsoft.Azure.WebJobs.Extensions, Version=\d.\d.\d.\d"),
            @"Microsoft.Azure.WebJobs.Extensions, Version=0.0.0.0");

        settings.AddRegexScrubber(new(@" in .+\.cs:line \d+"), string.Empty);

        filename ??= $"{nameof(AzureFunctionsTests)}.Isolated";
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    private static async Task WaitForFunctionHostHttpEndpointAsync(int port = 7071, int timeoutSeconds = 60)
    {
        // This only waits for the Functions host HTTP endpoint to accept requests. The ping action itself
        // does not report whether the script host and its trigger listeners have finished initializing.
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var pingUrl = $"http://127.0.0.1:{port}/admin/host/ping";
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync(pingUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Host not yet listening
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Azure Functions app did not become ready on port {port} within {timeoutSeconds} seconds.");
    }

#if NETCOREAPP3_1
    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class InProcessRuntimeV3 : AzureFunctionsTests
    {
        public InProcessRuntimeV3(ITestOutputHelper output)
            : base("AzureFunctions.V3InProcess", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~3");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            using (await RunAzureFunctionAndWaitForExit(agent))
            {
                const int expectedSpanCount = 21;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                var filteredSpans = spans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();

                using var s = new AssertionScope();
                filteredSpans.Should().HaveCount(expectedSpanCount);
                await AssertInProcessSpans(filteredSpans);
            }
        }
    }
#endif

#if NET6_0
    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class InProcessRuntimeV4 : AzureFunctionsTests
    {
        public InProcessRuntimeV4(ITestOutputHelper output)
            : base("AzureFunctions.V4InProcess", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true, useStatsD: true);

            using (await RunAzureFunctionAndWaitForExit(agent, framework: "net6.0"))
            {
                const int expectedSpanCount = 21;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                var filteredSpans = spans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();

                using var s = new AssertionScope();
                filteredSpans.Should().HaveCount(expectedSpanCount);
                await AssertInProcessSpans(filteredSpans);
            }
        }
    }
#endif

// v1 is only supported on .NET 6 - .NET 8 - they won't build on .NET 9
#if NET6_0 || NET7_0 || NET8_0
    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4SdkV1 : AzureFunctionsTests
    {
        public IsolatedRuntimeV4SdkV1(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated.SdkV1", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    const int expectedSpanCount = 21;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    using var s = new AssertionScope();
                    spans.Should().HaveCount(expectedSpanCount);
                    await AssertIsolatedSpans(spans, $"{nameof(AzureFunctionsTests)}.Isolated.V4.Sdk1");
                });
        }
    }

    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4AspNetCoreV1 : AzureFunctionsTests
    {
        public IsolatedRuntimeV4AspNetCoreV1(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated.AspNetCore.SdkV1", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    const int expectedSpanCount = 31;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    var filteredSpans = FilterOutSocketsHttpHandler(spans);

                    using var s = new AssertionScope();
                    spans.Should().HaveCount(expectedSpanCount);
                    await AssertIsolatedSpans(filteredSpans.ToImmutableList(), $"{nameof(AzureFunctionsTests)}.Isolated.V4.AspNetCore1");
                });
        }
    }
#endif

#if NET6_0_OR_GREATER
    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4 : AzureFunctionsTests
    {
        public IsolatedRuntimeV4(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            // by default host logs are disabled e.g.,
            // DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED=false
            // so we will enable them with a lot of logging
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED", "true");
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL", "VERBOSE");
            const string hostName = "integration_ilogger_az_tests";

            using var logsIntake = new MockLogsIntake();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    const int expectedSpanCount = 21;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    using var s = new AssertionScope();
                    spans.Should().HaveCount(expectedSpanCount);
                    await AssertIsolatedSpans(spans);
                });

            // The direct log submission sink can remain buffered until the worker shuts down.
            var logs = await WaitForLogsAsync(logsIntake, static logs => logs.Count >= 200);
            logs.Should().HaveCountGreaterThanOrEqualTo(200);
        }
    }

    // The reason why we have a separate application here is because we run into a Singleton locking issue when
    // we re-run the same function application in the same test session.
    // I couldn't find a way to reset the state between test runs, so the easiest solution was to
    // just create a separate function app.
    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4HostLogsDisabled : AzureFunctionsTests
    {
        public IsolatedRuntimeV4HostLogsDisabled(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated.HostLogsDisabled", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED", "false");
            SetEnvironmentVariable("DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL", "VERBOSE");
            const string hostName = "integration_ilogger_az_tests";

            using var logsIntake = new MockLogsIntake();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    const int expectedSpanCount = 21;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    using var s = new AssertionScope();
                    spans.Should().HaveCount(expectedSpanCount);
                    await AssertIsolatedSpans(spans, filename: $"{nameof(AzureFunctionsTests)}.Isolated.V4.HostLogsDisabled");
                });

            // Worker logs are submitted during shutdown, but the hundreds of host logs must remain disabled.
            // Wait for submissions to become quiet before checking the upper bound so a later batch cannot
            // invalidate the assertion after the first worker-log batch satisfies the lower bound.
            var logs = await WaitForLogsToStabilizeAsync(logsIntake, minimumCount: 11);
            logs.Should().HaveCountGreaterThan(10);
            logs.Should().HaveCountLessThanOrEqualTo(20);
        }
    }

    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4AspNetCore : AzureFunctionsTests
    {
        public IsolatedRuntimeV4AspNetCore(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated.AspNetCore", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    const int expectedSpanCount = 31;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    // There are _additional_ spans created for these compared to the non-AspNetCore version
                    // These are http-client-handler-type: System.Net.Http.SocketsHttpHandler that come in around
                    // the same time as some of the `azure_functions.invoke` spans
                    // because of this they cause a lot of flake in the snapshots where they shift places
                    // opting to just scrub them from the snapshots - we also don't think that the spans provide much
                    // value so they may be removed from being traced.
                    var filteredSpans = FilterOutSocketsHttpHandler(spans).ToImmutableList();

                    using var s = new AssertionScope();
                    spans.Should().HaveCount(expectedSpanCount);
                    await AssertIsolatedSpans(filteredSpans, $"{nameof(AzureFunctionsTests)}.Isolated.V4.AspNetCore");
                });
        }
    }

    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4AzureApim : AzureFunctionsTests
    {
        public IsolatedRuntimeV4AzureApim(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
            SetEnvironmentVariable("DD_TRACE_INFERRED_PROXY_SERVICES_ENABLED", "true");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesWithAzureApimHeaders()
        {
            // Enable APIM testing - this makes TriggerAllTimer call CallFunctionHttpWithProxy
            SetEnvironmentVariable("DD_TEST_APIM_ENABLED", "1");

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            SuppressReadinessPingSpans(agent);

            await RunIsolatedAzureFunctionAsync(
                agent,
                async () =>
                {
                    // 6 spans: Timer TriggerAllTimer, http.request, azure.apim, host span (GET /api/simple),
                    // worker span (Http SimpleHttpTrigger), Manual inside Simple.
                    const int expectedSpanCount = 6;
                    var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                    using var assertionScope = new AssertionScope();

                    spans.Count.Should().Be(expectedSpanCount);

                    spans.Should().ContainSingle(s => s.Name == "azure.apim");

                    // Verify we have azure_functions.invoke spans: host (GET /api/simple), worker (Http SimpleHttpTrigger), Timer TriggerAllTimer
                    var functionSpans = spans.Where(s => s.Name == "azure_functions.invoke").ToList();
                    functionSpans.Should().HaveCount(3);
                    functionSpans.Should().Contain(s => s.Resource.Contains("TriggerAllTimer"));
                    functionSpans.Should().Contain(s => s.Resource.Contains("SimpleHttpTrigger"));
                    functionSpans.Should().Contain(s => s.Resource.Contains("GET /api/simple"));

                    // Verify the http.request span
                    var httpSpan = spans.Should().ContainSingle(s => s.Name == "http.request").Subject;
                    httpSpan.Tags.Should().ContainKey("http.url");

                    var settings = VerifyHelper.GetSpanVerifierSettings();
                    settings.AddSimpleScrubber("aas.environment.runtime: .NET Core", "aas.environment.runtime: .NET");
                    settings.AddRegexScrubber(
                        new(@"Microsoft.Azure.WebJobs.Extensions, Version=\d.\d.\d.\d"),
                        @"Microsoft.Azure.WebJobs.Extensions, Version=0.0.0.0");
                    settings.AddRegexScrubber(new(@" in .+\.cs:line \d+"), string.Empty);
                    await VerifyHelper.VerifySpans(spans, settings)
                                      .UseFileName($"{nameof(AzureFunctionsTests)}.Isolated.V4.AzureApim")
                                      .DisableRequireUniquePrefix();
                });
        }
    }
#endif

    [CollectionDefinition(nameof(AzureFunctionsTestsCollection), DisableParallelization = true)]
    public class AzureFunctionsTestsCollection
    {
    }
}
