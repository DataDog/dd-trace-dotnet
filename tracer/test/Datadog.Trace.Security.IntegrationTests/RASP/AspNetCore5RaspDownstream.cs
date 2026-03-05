// <copyright file="AspNetCore5RaspDownstream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rasp;

public class AspNetCore5RaspDownstream : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5RaspDownstream(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5.RaspDownstream")
    {
        EnableRasp();
        SetSecurity(true);
        EnableIast(false);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, "rasp-rule-set.json");
        EnableEvidenceRedaction(false);

        // Configure downstream body analysis
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityDownstreamBodyAnalysisSampleRate, "1.0");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityMaxDownstreamRequestBodyAnalysis, "10");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.AppSecBodyParsingSizeLimit, "10000000");

        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
    }

    protected AspNetCoreTestFixture Fixture { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await Fixture.TryStartApp(this, true);
        SetHttpPort(Fixture.HttpPort);
    }

    [SkippableTheory]
    [InlineData("WithoutBody_CapturesRequestData", "/Rasp/DownstreamToSelf", null)]
    [InlineData("WithBody_ParsesAndCapturesBody", "/Rasp/DownstreamToSelf", "{\"test\":\"value\"}")]
    [InlineData("WithBodyQueryParam_CapturesBody", "/Rasp/DownstreamToSelf?body={\"cookie\":\"secret\"}", null)]
    [Trait("RunOnWindows", "True")]
    public async Task TestDownstreamRequest(string testName, string url, string body)
    {
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;

        var spans = await SendRequestsAsync(agent, url, body, 1, 1, string.Empty, "application/json", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(testName, string.Empty, string.Empty);
        await VerifySpans(spansFiltered.ToImmutableList(), settings);
    }
}

#endif
