// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
        SetEnvironmentVariable("DD_AZURE_APP_SERVICES", "1");
        SetEnvironmentVariable("DD_API_KEY", "NOT_SET"); // required for tracing to activate
        SetEnvironmentVariable("WEBSITE_SITE_NAME", "AzureFunctionsAllTriggers");
        SetEnvironmentVariable("COMPUTERNAME", "IntegrationTestHost");
        SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet");
        // Disable the process integration as func.exe calls out to `where`
        SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Process)}_ENABLED", "0");
        // Add an extra exclude for calls to storage emulator. These aren't necessary in production
        // as they are already covered by the existing excludes
        var nullAas = new AzureAppServices(new Dictionary<string, string>());
        SetEnvironmentVariable("DD_TRACE_HTTP_CLIENT_EXCLUDED_URL_SUBSTRINGS", nullAas.DefaultHttpClientExclusions + ", devstoreaccount1/azure-webjobs-hosts");
    }

    protected ProcessResult RunAzureFunctionAndWaitForExit(MockTracerAgent agent, string framework = null)
    {
        // run the azure function
        var binFolder = EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion: string.Empty, framework, usePublishFolder: false);
        Output.WriteLine("Using binFolder: " + binFolder);
        var process = ProfilerHelper.StartProcessWithProfiler(
            executable: "func",
            EnvironmentHelper,
            agent,
            "start --verbose",
            aspNetCorePort: 7071, // The default port
            processToProfile: null,
            workingDirectory: binFolder); // points to the sample project

        using var helper = new ProcessHelper(process);

        return WaitForProcessResult(helper);
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

#if NETCOREAPP3_1
    [UsesVerify]
    public class InProcessRuntimeV3 : AzureFunctionsTests
    {
        public InProcessRuntimeV3(ITestOutputHelper output)
            : base("AzureFunctions.V3InProcess", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunAzureFunctionAndWaitForExit(agent))
            {
                const int expectedSpanCount = 9;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                await AssertInProcessSpans(spans);
            }
        }
    }
#endif

#if NET6_0
    [UsesVerify]
    public class InProcessRuntimeV4 : AzureFunctionsTests
    {
        public InProcessRuntimeV4(ITestOutputHelper output)
            : base("AzureFunctions.V4InProcess", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "AzureFunctions")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunAzureFunctionAndWaitForExit(agent, framework: "net6.0"))
            {
                const int expectedSpanCount = 9;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                await AssertInProcessSpans(spans);
            }
        }
    }
#endif
}
