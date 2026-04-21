// <copyright file="AspNetWebFormsJwtTest.cs" company="Datadog">
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
public class AspNetWebFormsJwtEnabled : AspNetWebFormsJwtTest
{
    public AspNetWebFormsJwtEnabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableSecurity: true)
    {
    }
}

/// <summary>
/// Diagnostic test: sends a JWT with a path-traversal attack in the "name" claim to a
/// plain ASP.NET WebForms endpoint. Logs whether the WAF decoded the JWT and triggered
/// an AppSec event. Uses the built-in production ruleset (no DD_APPSEC_RULES override).
/// </summary>
public abstract class AspNetWebFormsJwtTest : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    // JWT payload: {"sub":"1234567890","name":"../../../../../../../etc/passwd","iat":1516239022}
    private const string AttackPayload = "../../../../../../../etc/passwd";
    private const string JwtWithAttackInNameClaim =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9."
      + "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6Ii4uLy4uLy4uLy4uLy4uLy4uLy4uL2V0Yy9wYXNzd2QiLCJpYXQiOjE1MTYyMzkwMjJ9."
      + "PxH-A57-BDG5aiJgq8CslxQvxUVKKHeGQETBRCrJgp4";

    private readonly IisFixture _iisFixture;

    internal AspNetWebFormsJwtTest(IisFixture iisFixture, ITestOutputHelper output, bool enableSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet", allowAutoRedirect: false)
    {
        _iisFixture = iisFixture;

        SetSecurity(enableSecurity);
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _testName = "Security." + nameof(AspNetWebFormsJwtTest) + ".enableSecurity=" + enableSecurity;
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Health?arg=[$slice]")]
    public async Task TestJwtClaimsProcessedByWaf(string url)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        var jwtHeaders = new[] { new KeyValuePair<string, string>("Authorization", "Bearer " + JwtWithAttackInNameClaim) };
        await SubmitRequest(url, body: null, contentType: string.Empty, headers: jwtHeaders);

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.FirstOrDefault(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains("/Health"));
        requestSpan.Should().NotBeNull("the request to /Health should produce a span");

        var wafRan = requestSpan.Metrics.ContainsKey("_dd.appsec.waf.duration");
        Output.WriteLine($"[jwt] WAF reported result: {wafRan}");

        string appSecJson = null;
        if (requestSpan.Tags.TryGetValue(Tags.AppSecJson, out var json))
        {
            appSecJson = json;
        }
        else if (requestSpan.MetaStruct != null && requestSpan.MetaStruct.TryGetValue("appsec", out var metaStruct))
        {
            appSecJson = MetaStructToJson(metaStruct);
        }

        Output.WriteLine($"[jwt] AppSec JSON present: {!string.IsNullOrEmpty(appSecJson)}");
        if (!string.IsNullOrEmpty(appSecJson))
        {
            var hasAttack = appSecJson.Contains(AttackPayload);
            Output.WriteLine($"[jwt] Contains decoded JWT attack payload: {hasAttack}");
            Output.WriteLine($"[jwt] AppSec JSON snippet: {appSecJson.Substring(0, Math.Min(500, appSecJson.Length))}");
        }

        Output.WriteLine($"[jwt] Span tags: {string.Join(", ", requestSpan.Tags.Keys)}");

        wafRan.Should().BeTrue("the [$slice] attack in the URL guarantees the WAF fires");
        appSecJson.Should().NotBeNullOrWhiteSpace("the [$slice] attack pattern should trigger an AppSec event");
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
