// <copyright file="AspNetWebFormsApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity;

[Collection("IisTests")]
public class AspNetWebFormsApiSecurityEnabled : AspNetWebFormsApiSecurity
{
    public AspNetWebFormsApiSecurityEnabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableApiSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsApiSecurityDisabled : AspNetWebFormsApiSecurity
{
    public AspNetWebFormsApiSecurityDisabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableApiSecurity: false)
    {
    }
}

/// <summary>
/// Tests whether API Security schema extraction fires for plain ASP.NET WebForms.
/// Hypothesis: TracingHttpModule uses WebTags (no HttpRoute property), so http.route is never
/// set on the span, causing ApiSecurity.ShouldAnalyzeSchema to return false.
/// These tests prove or disprove this for two routing mechanisms: IHttpHandler routes and MapPageRoute.
/// </summary>
public abstract class AspNetWebFormsApiSecurity : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private const string RequestBodySchemaTag = "_dd.appsec.s.req.body";
    private const string HttpRouteTag = "http.route";

    private readonly bool _enableApiSecurity;
    private readonly IisFixture _iisFixture;

    internal AspNetWebFormsApiSecurity(IisFixture iisFixture, ITestOutputHelper output, bool enableApiSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet", allowAutoRedirect: false)
    {
        _enableApiSecurity = enableApiSecurity;
        _iisFixture = iisFixture;

        SetSecurity(true);
        EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.Rules, "ApiSecurity\\ruleset-with-block.json");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityEnabled, enableApiSecurity.ToString());

        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _testName = "Security." + nameof(AspNetWebFormsApiSecurity) + ".enableApiSecurity=" + enableApiSecurity;
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("handler-route", "/api/security/12", """{"Dog":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    public async Task TestApiSecurityHandlerRoute(string scenario, string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        var result = await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        Output.WriteLine($"[handler-route] http.route present: {hasRoute}");
        if (hasRoute)
        {
            Output.WriteLine($"[handler-route] http.route value: {requestSpan.Tags[HttpRouteTag]}");
        }

        // When http.route is absent, ShouldAnalyzeSchema returns false regardless of settings
        if (_enableApiSecurity && hasRoute)
        {
            requestSpan.Tags.Should().ContainKey(RequestBodySchemaTag,
                because: "http.route is present and API Security is enabled, so schema extraction should fire");
        }
        else
        {
            requestSpan.Tags.Should().NotContainKey(RequestBodySchemaTag,
                because: hasRoute
                    ? "API Security is disabled"
                    : "http.route is missing so ShouldAnalyzeSchema returns false");
        }
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("mapped-page-route", "/Health/Params/12", """{"Dog":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    public async Task TestApiSecurityMappedPageRoute(string scenario, string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        var result = await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        Output.WriteLine($"[mapped-page-route] http.route present: {hasRoute}");
        if (hasRoute)
        {
            Output.WriteLine($"[mapped-page-route] http.route value: {requestSpan.Tags[HttpRouteTag]}");
        }

        if (_enableApiSecurity && hasRoute)
        {
            requestSpan.Tags.Should().ContainKey(RequestBodySchemaTag,
                because: "http.route is present and API Security is enabled, so schema extraction should fire");
        }
        else
        {
            requestSpan.Tags.Should().NotContainKey(RequestBodySchemaTag,
                because: hasRoute
                    ? "API Security is disabled"
                    : "http.route is missing so ShouldAnalyzeSchema returns false");
        }
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
        await SubmitRequest("/", null, string.Empty);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override string GetTestName() => _testName;
}
#endif
