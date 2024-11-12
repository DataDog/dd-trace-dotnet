// <copyright file="GrpcDotNetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.IAST.GrpcDotNet;

[UsesVerify]
public class GrpcDotNetTests : TestHelper
{
    public GrpcDotNetTests(ITestOutputHelper output)
        : base("GrpcDotNet", output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.TelemetryVerbosity, "Off");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "200");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "1");

        SetEnvironmentVariable("IAST_GRPC_SOURCE_TEST", "1");
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        GuardAlpine();
        SkipOn.Platform(SkipOn.PlatformValue.Linux);

        const int expectedSpanCount = 24;
        const string filename = "Iast.GrpcDotNetTests.BodyPropagation.SubmitsTraces";
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();

        // Add scrub for the location data, as using APM sample, we won't disable symbols on their sample
        (Regex RegexPattern, string Replacement) locationMsgRegex = (new Regex(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,4}(\s)*},"), string.Empty);
        settings.AddRegexScrubber(locationMsgRegex);

        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }

    private static void GuardAlpine()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
        {
            throw new SkipException("GRPC.Tools does not support Alpine");
        }
    }
}
#endif
