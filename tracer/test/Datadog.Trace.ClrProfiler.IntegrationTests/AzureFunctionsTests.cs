// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
    protected AzureFunctionsTests(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides: Path.Combine("test", "test-applications", "azure-functions"), output)
    {
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

    protected async Task<ProcessResult> RunAzureFunctionAndWaitForExit(MockTracerAgent agent, string framework = null, int expectedExitCode = 0)
    {
        // run the azure function
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

        using var helper = new ProcessHelper(process);

        return WaitForProcessResult(helper, expectedExitCode);
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
                filteredSpans.Count.Should().Be(expectedSpanCount);

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
            using (await RunAzureFunctionAndWaitForExit(agent, expectedExitCode: -1))
            {
                const int expectedSpanCount = 21;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                var filteredSpans = spans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();
                using var s = new AssertionScope();

                await AssertIsolatedSpans(filteredSpans, $"{nameof(AzureFunctionsTests)}.Isolated.V4.Sdk1");
            }
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
            using (await RunAzureFunctionAndWaitForExit(agent, expectedExitCode: -1))
            {
                const int expectedSpanCount = 26;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                var filteredSpans = FilterOutSocketsHttpHandler(spans);

                using var s = new AssertionScope();

                await AssertIsolatedSpans(filteredSpans.ToImmutableList(), $"{nameof(AzureFunctionsTests)}.Isolated.V4.AspNetCore1");

                spans.Count.Should().Be(expectedSpanCount);
            }
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
            var hostName = "integration_ilogger_az_tests";
            using var logsIntake = new MockLogsIntake();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using (await RunAzureFunctionAndWaitForExit(agent, expectedExitCode: -1))
            {
                const int expectedSpanCount = 21;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                var filteredSpans = spans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();

                using var s = new AssertionScope();
                await AssertIsolatedSpans(filteredSpans);

                filteredSpans.Count.Should().Be(expectedSpanCount);

                var logs = logsIntake.Logs;

                // ~327 (ish) logs but we kill func.exe so some logs are lost
                // and since sometimes the batch of logs can be 100+ it can be a LOT of logs that we lose
                // so just check that we have much more than when we have host logs disabled
                logs.Should().HaveCountGreaterThanOrEqualTo(200);
            }
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
            var hostName = "integration_ilogger_az_tests";
            using var logsIntake = new MockLogsIntake();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.ILogger), hostName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using (await RunAzureFunctionAndWaitForExit(agent, expectedExitCode: -1))
            {
                const int expectedSpanCount = 21;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                var filteredSpans = spans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();

                await AssertIsolatedSpans(filteredSpans, filename: $"{nameof(AzureFunctionsTests)}.Isolated.V4.HostLogsDisabled");
                filteredSpans.Count.Should().Be(expectedSpanCount);

                var logs = logsIntake.Logs;
                // we expect some logs still from the worker process
                // this just seems flaky I THINK because of killing the func.exe process (even though we aren't using the host logs)
                // commonly see 13, 14, 15, 16 logs, but IF we were logging the host logs we'd see 300+
                logs.Should().HaveCountGreaterThan(10);
                logs.Should().HaveCountLessThanOrEqualTo(20);
            }
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
            using (await RunAzureFunctionAndWaitForExit(agent, expectedExitCode: -1))
            {
                const int expectedSpanCount = 26;
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);

                // There are _additional_ spans created for these compared to the non-AspNetCore version
                // These are http-client-handler-type: System.Net.Http.SocketsHttpHandler that come in around
                // the same time as some of the `azure_functions.invoke` spans
                // because of this they cause a lot of flake in the snapshots where they shift places
                // opting to just scrub them from the snapshots - we also don't think that the spans provide much
                // value so they may be removed from being traced.
                var filteredSpans = FilterOutSocketsHttpHandler(spans);

                filteredSpans = filteredSpans.Where(s => !s.Resource.Equals("Timer ExitApp", StringComparison.OrdinalIgnoreCase)).ToImmutableList();

                using var s = new AssertionScope();

                await AssertIsolatedSpans(filteredSpans.ToImmutableList(), $"{nameof(AzureFunctionsTests)}.Isolated.V4.AspNetCore");

                spans.Count.Should().Be(expectedSpanCount);
            }
        }
    }

    [UsesVerify]
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class IsolatedRuntimeV4InferredProxySpans : AzureFunctionsTests
    {
        public IsolatedRuntimeV4InferredProxySpans(ITestOutputHelper output)
            : base("AzureFunctions.V4Isolated", output)
        {
            SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, "true");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesWithProxySpan()
        {
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            // Start the Azure Function
            var binFolder = EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion: string.Empty, framework: null);
            Output.WriteLine("Using binFolder: " + binFolder);
            var process = await ProfilerHelper.StartProcessWithProfiler(
                executable: "func",
                EnvironmentHelper,
                agent,
                "start --verbose",
                aspNetCorePort: 7071,
                processToProfile: null,
                workingDirectory: binFolder);

            using var helper = new ProcessHelper(process);

            try
            {
                // Wait for the function to be ready (longer wait to ensure it's fully started)
                await Task.Delay(TimeSpan.FromSeconds(15));

                // Create HTTP client and request with Azure APIM proxy headers
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:7071/api/simple");

                var startTime = DateTimeOffset.UtcNow;
                request.Headers.Add("x-dd-proxy", "azure-apim");
                request.Headers.Add("x-dd-proxy-request-time-ms", startTime.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                request.Headers.Add("x-dd-proxy-domain-name", "test-apim.azure-api.net");
                request.Headers.Add("x-dd-proxy-httpmethod", "GET");
                request.Headers.Add("x-dd-proxy-path", "/api/test");

                // Send the request
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Get all spans and filter to those from our request
                var allSpans = agent.Spans.ToImmutableList();

                // Find spans that include the proxy span - they should be recent
                var recentSpans = allSpans.Where(s => s.Start >= startTime.ToUnixTimeMilliseconds() * 1000).ToList();

                using var assertionScope = new AssertionScope();

                // Verify proxy span exists
                var proxySpan = recentSpans.FirstOrDefault(span => span.Name == "azure-apim.request");
                proxySpan.Should().NotBeNull("proxy span should be created when proxy headers are present");

                if (proxySpan is not null)
                {
                    proxySpan.Tags.Should().ContainKey("span.kind").WhoseValue.Should().Be("proxy");
                    proxySpan.Tags.Should().ContainKey("http.url").WhoseValue.Should().Contain("test-apim.azure-api.net");

                    // Verify azure functions span exists and is a child of proxy span
                    var azureFunctionSpan = recentSpans.FirstOrDefault(span =>
                        span.Name == "azure_functions.invoke" &&
                        span.ParentId == proxySpan.SpanId);
                    azureFunctionSpan.Should().NotBeNull("azure functions span should be a child of the proxy span");
                }
            }
            finally
            {
                helper.Process?.Kill();
            }
        }
    }
#endif

    [CollectionDefinition(nameof(AzureFunctionsTestsCollection), DisableParallelization = true)]
    public class AzureFunctionsTestsCollection
    {
    }
}
