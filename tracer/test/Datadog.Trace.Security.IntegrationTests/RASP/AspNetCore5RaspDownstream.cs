// <copyright file="AspNetCore5RaspDownstream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rasp;

public class AspNetCore5RaspDownstream : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private const string ResponseBodyReadableTestName = "ResponseBodyReadable";

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

        var spans = await SendRequestsAsync(agent, url, body, 1, 5, string.Empty, "application/json", null);
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseParameters(testName, string.Empty, string.Empty);
        await VerifySpans(spansFiltered, settings, orderSpans: OrderSpans);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestDownstreamResponseBodyReadableAfterAnalysis()
    {
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var minDateTime = DateTime.UtcNow;

        var response = await SubmitRequest("/Rasp/DownstreamToSelf", null, "application/json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(response.ResponseText);
        var root = document.RootElement;
        root.TryGetProperty("error", out _).Should().BeFalse(response.ResponseText);
        root.GetProperty("statusCode").GetInt32().Should().Be((int)HttpStatusCode.OK);
        root.GetProperty("body").GetString().Should().Contain("defaultBody");

        var spans = await WaitForSpansAsync(Fixture.Agent, 5, ResponseBodyReadableTestName, minDateTime, "/Rasp/DownstreamToSelf");
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        await VerifySpans(
            spansFiltered,
            settings,
            fileNameOverride: $"{_testName}.__testName={ResponseBodyReadableTestName}_url=_body=",
            orderSpans: OrderSpans);
    }

    private static IOrderedEnumerable<MockSpan> OrderSpans(IReadOnlyCollection<MockSpan> spans)
        => spans
              .OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
              .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
              .ThenBy(x => x.Resource)
              .ThenBy(x => x.Start)
              .ThenBy(x => x.Duration);
}

#endif
