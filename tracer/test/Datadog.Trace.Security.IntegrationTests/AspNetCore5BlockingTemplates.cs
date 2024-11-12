// <copyright file="AspNetCore5BlockingTemplates.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests;

public abstract class AspNetCore5BlockingTemplates : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5BlockingTemplates(
        string sampleName,
        AspNetCoreTestFixture fixture,
        ITestOutputHelper outputHelper,
        string shutdownPath,
        string testName = null)
        : base(
            sampleName,
            outputHelper,
            shutdownPath ?? "/shutdown",
            testName: testName)
    {
        Fixture = fixture;
        Fixture.SetOutput(outputHelper);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.HtmlBlockedTemplate, "blocking-template1.json");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.JsonBlockedTemplate, "blocking-template2.json");
    }

    protected AspNetCoreTestFixture Fixture { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await Fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: AppDomain.CurrentDomain.BaseDirectory + DefaultRuleFile);
        SetHttpPort(Fixture.HttpPort);
    }
}

public class AspNetCore5BlockingTemplatesHtml : AspNetCore5BlockingTemplates
{
    public AspNetCore5BlockingTemplatesHtml(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", fixture, outputHelper, "/shutdown", testName: "AspNetCore5.SecurityBlockingTemplatesHtml")
    {
    }

    [SkippableTheory]
    [InlineData(AddressesConstants.RequestUriRaw, HttpStatusCode.Forbidden, "/health?q=fun")]
    [Trait("RunOnWindows", "True")]
    public async Task TestGet(string test, HttpStatusCode expectedStatusCode, string url)
    {
        VerifierSettings.DisableRequireUniquePrefix();
        await TryStartApp();
        var agent = Fixture.Agent;

        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);
        var minDateTime = DateTime.UtcNow;
        var (x, page) = await SubmitRequest(url, body: null, contentType: null, accept: "text/html");
        page.Should().Be(@"<!DOCTYPE html>
<html lang=""en""></html>");
        var spans = WaitForSpans(agent, 1, string.Empty, minDateTime, url);
        await VerifySpans(spans, settings);
    }
}

public class AspNetCore5BlockingTemplatesJson : AspNetCore5BlockingTemplates
{
    public AspNetCore5BlockingTemplatesJson(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", fixture, outputHelper, "/shutdown", testName: "AspNetCore5.SecurityBlockingTemplatesJson")
    {
    }

    [SkippableTheory]
    [InlineData(AddressesConstants.RequestUriRaw, HttpStatusCode.Forbidden, "/Home/Privacy?q=fun")]
    [Trait("RunOnWindows", "True")]
    public async Task TestGet(string test, HttpStatusCode expectedStatusCode, string url)
    {
        VerifierSettings.DisableRequireUniquePrefix();
        await TryStartApp();
        var agent = Fixture.Agent;

        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);
        var minDateTime = DateTime.UtcNow;
        var (x, page) = await SubmitRequest(url, body: null, contentType: null);
        page.Should().Be("{}");
        var spans = WaitForSpans(agent, 1, string.Empty, minDateTime, url);
        await VerifySpans(spans, settings);
    }
}
#endif
