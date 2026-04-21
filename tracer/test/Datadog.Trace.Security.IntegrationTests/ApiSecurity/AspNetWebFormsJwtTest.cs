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

[Collection("IisTests")]
public class AspNetWebFormsJwtDisabled : AspNetWebFormsJwtTest
{
    public AspNetWebFormsJwtDisabled(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, enableSecurity: false)
    {
    }
}

/// <summary>
/// Tests whether JWT claim extraction works for plain ASP.NET WebForms via the shared
/// SecurityCoordinator.Framework path that feeds all request headers into the WAF.
/// The JWT contains a path-traversal attack in the "name" claim; if the WAF decodes
/// the JWT, it should trigger an AppSec event containing the decoded attack value.
/// </summary>
public abstract class AspNetWebFormsJwtTest : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    // JWT payload: {"sub":"1234567890","name":"../../../../../../../etc/passwd","iat":1516239022}
    private const string AttackPayload = "../../../../../../../etc/passwd";
    private const string JwtWithAttackInNameClaim =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9."
      + "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6Ii4uLy4uLy4uLy4uLy4uLy4uLy4uL2V0Yy9wYXNzd2QiLCJpYXQiOjE1MTYyMzkwMjJ9."
      + "PxH-A57-BDG5aiJgq8CslxQvxUVKKHeGQETBRCrJgp4";

    private readonly bool _enableSecurity;
    private readonly IisFixture _iisFixture;

    internal AspNetWebFormsJwtTest(IisFixture iisFixture, ITestOutputHelper output, bool enableSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet", allowAutoRedirect: false)
    {
        _enableSecurity = enableSecurity;
        _iisFixture = iisFixture;

        SetSecurity(enableSecurity);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _testName = "Security." + nameof(AspNetWebFormsJwtTest) + ".enableSecurity=" + enableSecurity;
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("/Health")]
    public async Task TestJwtClaimsProcessedByWaf(string url)
    {
        var agent = _iisFixture.Agent;
        var dateTime = DateTime.UtcNow;
        var jwtHeaders = new[] { new KeyValuePair<string, string>("Authorization", "Bearer " + JwtWithAttackInNameClaim) };
        await SubmitRequest(url, body: null, contentType: string.Empty, headers: jwtHeaders);

        var spans = await agent.WaitForSpansAsync(1, minDateTime: dateTime);
        var requestSpan = spans.First(s => s.Tags.TryGetValue("http.url", out var u) && u.Contains(url));

        Output.WriteLine($"[jwt] WAF ran: {requestSpan.Metrics.ContainsKey("_dd.appsec.waf.duration")}");

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
            Output.WriteLine($"[jwt] Contains attack payload: {appSecJson.Contains(AttackPayload)}");
        }

        if (_enableSecurity)
        {
            requestSpan.Metrics.Should().ContainKey(
                "_dd.appsec.waf.duration",
                "the WAF should run when AppSec is enabled");
            appSecJson.Should().NotBeNullOrWhiteSpace(
                "the decoded JWT claim contains a path-traversal attack that should trigger an AppSec event");
            appSecJson.Should().Contain(
                AttackPayload,
                "the event should reference the decoded JWT claim value, proving JWT extraction happened");
        }
        else
        {
            appSecJson.Should().BeNullOrEmpty(
                "AppSec events should not appear when AppSec is disabled");
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
