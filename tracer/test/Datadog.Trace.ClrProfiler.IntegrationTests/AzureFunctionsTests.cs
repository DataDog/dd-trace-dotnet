// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
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

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                await AssertInProcessSpans(spans);
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

                using var s = new AssertionScope();

                await AssertIsolatedSpans(spans, $"{nameof(AzureFunctionsTests)}.Isolated.V4.Sdk1");
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

                using var s = new AssertionScope();

                await AssertIsolatedSpans(spans);

                spans.Count.Should().Be(expectedSpanCount);
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
                // the same time as some of the `azure-functions.invoke` spans
                // because of this they cause a lot of flake in the snapshots where they shift places
                // opting to just scrub them from the snapshots - we also don't think that the spans provide much
                // value so they may be removed from being traced.
                var filteredSpans = FilterOutSocketsHttpHandler(spans);

                using var s = new AssertionScope();

                await AssertIsolatedSpans(filteredSpans.ToImmutableList(), $"{nameof(AzureFunctionsTests)}.Isolated.V4.AspNetCore");

                spans.Count.Should().Be(expectedSpanCount);
            }
        }
    }
#endif

    [CollectionDefinition(nameof(AzureFunctionsTestsCollection), DisableParallelization = true)]
    public class AzureFunctionsTestsCollection
    {
    }
}
