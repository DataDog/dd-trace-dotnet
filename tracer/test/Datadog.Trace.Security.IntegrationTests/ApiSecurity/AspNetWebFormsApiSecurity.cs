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
/// Hypothesis 1 (request body): TracingHttpModule uses WebTags (no HttpRoute property), so
/// http.route is never set on the span, causing ApiSecurity.ShouldAnalyzeSchema to return false.
/// Hypothesis 2 (response body): NetFx response-body capture is implemented only by two CallTarget
/// hooks — ControllerActionInvoker.InvokeActionMethod (MVC5, duck-cast to IJsonResultMvc) and
/// ReflectedHttpActionDescriptor.ExecuteAsync (WebApi2, duck-cast to IJsonResultWebApi). Neither
/// hook fires for an .aspx page or an IHttpHandler, so `server.response.body` can never be pushed
/// to the WAF on plain WebForms, even when http.route is present. These tests prove or disprove
/// both hypotheses for two routing mechanisms: IHttpHandler routes and MapPageRoute.
/// </summary>
public abstract class AspNetWebFormsApiSecurity : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private const string RequestBodySchemaTag = "_dd.appsec.s.req.body";
    private const string ResponseBodySchemaTag = "_dd.appsec.s.res.body";
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
    [InlineData("/api/security/12", """{"Dog":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    public async Task TestApiSecurityHandlerRoute(string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        Output.WriteLine($"[handler-route] http.route present: {hasRoute}");
        if (hasRoute)
        {
            Output.WriteLine($"[handler-route] http.route value: {requestSpan.Tags[HttpRouteTag]}");
        }

        AssertSchemaTagPresence(requestSpan, hasRoute);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Health/Params/12", """{"Dog":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    public async Task TestApiSecurityMappedPageRoute(string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        Output.WriteLine($"[mapped-page-route] http.route present: {hasRoute}");
        if (hasRoute)
        {
            Output.WriteLine($"[mapped-page-route] http.route value: {requestSpan.Tags[HttpRouteTag]}");
        }

        AssertSchemaTagPresence(requestSpan, hasRoute);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/api/security/12", """{"Dog":"23", "Dog2":"test"}""")]
    public async Task TestResponseBodyHandlerRoute(string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        var hasResponseBodySchema = requestSpan.Tags.ContainsKey(ResponseBodySchemaTag);
        Output.WriteLine($"[res-body handler-route] http.route present: {hasRoute}, _dd.appsec.s.res.body present: {hasResponseBodySchema}");

        AssertResponseBodySchemaTagPresence(requestSpan, hasRoute);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Health/Params/12", """{"Dog":"23", "Dog2":"test"}""")]
    public async Task TestResponseBodyMappedPageRoute(string url, string body)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        await SubmitRequest(url, body, "application/json");

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        var hasRoute = requestSpan.Tags.ContainsKey(HttpRouteTag);
        var hasResponseBodySchema = requestSpan.Tags.ContainsKey(ResponseBodySchemaTag);
        Output.WriteLine($"[res-body mapped-page-route] http.route present: {hasRoute}, _dd.appsec.s.res.body present: {hasResponseBodySchema}");

        AssertResponseBodySchemaTagPresence(requestSpan, hasRoute);
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
        await SubmitRequest("/", null, string.Empty);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override string GetTestName() => _testName;

    private void AssertSchemaTagPresence(MockSpan requestSpan, bool hasRoute)
    {
        if (_enableApiSecurity && hasRoute)
        {
            requestSpan.Tags.Should().ContainKey(
                RequestBodySchemaTag,
                "http.route is present and API Security is enabled, so schema extraction should fire");
        }
        else if (hasRoute)
        {
            requestSpan.Tags.Should().NotContainKey(
                RequestBodySchemaTag,
                "API Security is disabled");
        }
        else
        {
            requestSpan.Tags.Should().NotContainKey(
                RequestBodySchemaTag,
                "http.route is missing so ShouldAnalyzeSchema returns false");
        }
    }

    private void AssertResponseBodySchemaTagPresence(MockSpan requestSpan, bool hasRoute)
    {
        requestSpan.Tags.Should().NotContainKey(
            ResponseBodySchemaTag,
            hasRoute
                ? "plain WebForms has no MVC ActionResult / WebApi HttpActionDescriptor hook, so server.response.body is never pushed to the WAF even when http.route is present"
                : "http.route is missing so ShouldAnalyzeSchema short-circuits before any response-body analysis");
    }
}
#endif
