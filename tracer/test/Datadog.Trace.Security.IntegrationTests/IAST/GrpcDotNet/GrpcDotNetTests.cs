// <copyright file="GrpcDotNetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.IAST;

[UsesVerify]
public class GrpcDotNetTests : TestHelper
{
    public GrpcDotNetTests(ITestOutputHelper output)
        : base("Security.GrpcDotNet", output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.Iast.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.TelemetryVerbosity, "0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        const int expectedSpanCount = 10;
        var filename = "Iast.GrpcDotNetTests.BodyPropagation.SubmitsTraces";
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spansFiltered, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();
    }
}
