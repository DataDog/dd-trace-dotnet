// <copyright file="AspNetWebApiIastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.IAST;

[Collection("IisTests")]
public class AspNetWebApiIntegratedWithIast : AspNetWebApiIastTests
{
    public AspNetWebApiIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebApiClassicWithIast : AspNetWebApiIastTests
{
    public AspNetWebApiClassicWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true)
    {
    }
}

public abstract class AspNetWebApiIastTests : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _classicMode;

    protected AspNetWebApiIastTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode)
        : base("WebApi", output, "/api/home/shutdown", @"test\test-applications\security\aspnet", allowAutoRedirect: false)
    {
        EnableIast(true);
        EnableEvidenceRedaction(false);
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "false");

        _iisFixture = iisFixture;
        _classicMode = classicMode;
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsSqlInjectionFromQuery()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/SqlQuery?username=Vicent");

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "SQL_INJECTION");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "SqlQuery");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.parameter", "username", "Vicent");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsPathTraversalFromBody()
    {
        var (_, _, iastJson) = await SendIastRequestAsync(
            "/Iast/PathTraversal",
            body: "{\"Id\":\"nonexisting.txt\"}",
            contentType: "application/json");

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "PATH_TRAVERSAL");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "PathTraversal");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.body", value: "nonexisting.txt");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsCommandInjectionFromQuery()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1");

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "COMMAND_INJECTION");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "ExecuteCommandInternal");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.parameter", "file", "nonexisting.exe");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.parameter", "argumentLine", "arg1");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsCommandInjectionFromHeaders()
    {
        var (_, _, iastJson) = await SendIastRequestAsync(
            "/Iast/ExecuteCommandFromHeader",
            headers: new Dictionary<string, string>
            {
                { "file", "nonexisting.exe" },
                { "argumentLine", "arg1" },
            });

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "COMMAND_INJECTION");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "ExecuteCommandInternal");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.header", "file", "nonexisting.exe");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.header", "argumentLine", "arg1");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsCommandInjectionFromCookies()
    {
        var (_, _, iastJson) = await SendIastRequestAsync(
            "/Iast/ExecuteCommandFromCookie",
            cookies: new Dictionary<string, string>
            {
                { "file", "nonexisting.exe" },
                { "argumentLine", "arg1" },
            });

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "COMMAND_INJECTION");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "ExecuteCommandInternal");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.cookie.value", "file", "nonexisting.exe");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.cookie.value", "argumentLine", "arg1");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsLdapInjectionFromQuery()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/Ldap?userName=Vicent&skipQueryExecution=true");

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "LDAP_INJECTION");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "Ldap");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.parameter", "userName", "Vicent");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsHeaderInjectionFromHeaders()
    {
        var (_, _, iastJson) = await SendIastRequestAsync(
            "/Iast/HeaderInjection?useValueFromOriginHeader=false",
            headers: new Dictionary<string, string>
            {
                { "name", "Name" },
                { "value", "value" },
            });

        AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "HEADER_INJECTION");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.header", "name", "Name");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.header", "value", "value");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsCookieSecurityVulnerabilities()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/AllVulnerabilitiesCookie");

        var vulnerabilities = AspNetWebApiIastAssertions.GetVulnerabilities(iastJson).ToList();
        var vulnerabilityTypes = vulnerabilities.Select(v => v["type"]?.Value<string>()).ToList();
        vulnerabilityTypes.Should().Contain("NO_SAMESITE_COOKIE");
        vulnerabilityTypes.Should().Contain("NO_HTTPONLY_COOKIE");
        vulnerabilityTypes.Should().Contain("INSECURE_COOKIE");
        AspNetWebApiIastAssertions.AssertCookieEvidence(vulnerabilities, "NO_SAMESITE_COOKIE");
        AspNetWebApiIastAssertions.AssertCookieEvidence(vulnerabilities, "NO_HTTPONLY_COOKIE");
        AspNetWebApiIastAssertions.AssertCookieEvidence(vulnerabilities, "INSECURE_COOKIE");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsUnvalidatedRedirect()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/UnvalidatedRedirect?param=value");

        var vulnerability = AspNetWebApiIastAssertions.GetSingleVulnerability(iastJson, "UNVALIDATED_REDIRECT");
        AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "UnvalidatedRedirect");
        AspNetWebApiIastAssertions.AssertSource(iastJson, "http.request.parameter", "param", "value");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableFact]
    public async Task DetectsWeakHashing()
    {
        var (_, _, iastJson) = await SendIastRequestAsync("/Iast/WeakHashing");

        var vulnerabilities = AspNetWebApiIastAssertions.GetVulnerabilities(iastJson, "WEAK_HASH").ToList();
        vulnerabilities.Should().HaveCount(2);
        vulnerabilities.Select(v => v["evidence"]?["value"]?.Value<string>())
                       .Should()
                       .BeEquivalentTo(new[] { "MD5", "SHA1" });

        foreach (var vulnerability in vulnerabilities)
        {
            AspNetWebApiIastAssertions.AssertLocation(vulnerability, "Samples.Security.WebApi.Controllers.IastController", "WeakHashing");
        }
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(MockSpan RootSpan, MockSpan WebApiSpan, JObject IastJson)> SendIastRequestAsync(
        string url,
        string body = null,
        string contentType = null,
        IDictionary<string, string> headers = null,
        IDictionary<string, string> cookies = null)
    {
        if (headers != null)
        {
            AddHeaders(new Dictionary<string, string>(headers));
        }

        if (cookies != null)
        {
            AddCookies(new Dictionary<string, string>(cookies));
        }

        var spans = body == null
                        ? await SendRequestsAsync(_iisFixture.Agent, 2, new[] { url })
                        : await SendRequestsAsync(_iisFixture.Agent, url, body, 1, 2, string.Empty, contentType ?? "application/json");

        var rootSpan = spans.Single(span => span.Name == "aspnet.request" && span.ParentId == null);
        var webApiSpan = spans.Single(span => span.Name == "aspnet-webapi.request" && span.ParentId == rootSpan.SpanId);

        webApiSpan.ParentId.Should().Be(rootSpan.SpanId);
        rootSpan.GetTag(Tags.IastEnabled).Should().Be("1");

        if (rootSpan.MetaStruct is not null)
        {
            IastVerifyScrubberExtensions.IastMetaStructScrubbing(rootSpan);
        }

        var iastJsonText = rootSpan.GetTag(Tags.IastJson);
        iastJsonText.Should().NotBeNullOrEmpty();

        return (rootSpan, webApiSpan, JObject.Parse(iastJsonText));
    }
}

internal static class AspNetWebApiIastAssertions
{
    internal static IEnumerable<JObject> GetVulnerabilities(JObject iastJson)
    {
        var vulnerabilities = iastJson["vulnerabilities"] as JArray;
        vulnerabilities.Should().NotBeNull();
        return vulnerabilities!.OfType<JObject>();
    }

    internal static IEnumerable<JObject> GetVulnerabilities(JObject iastJson, string vulnerabilityType)
    {
        return GetVulnerabilities(iastJson).Where(vulnerability =>
            string.Equals(vulnerability["type"]?.Value<string>(), vulnerabilityType, StringComparison.Ordinal));
    }

    internal static JObject GetSingleVulnerability(JObject iastJson, string vulnerabilityType)
    {
        var vulnerabilities = GetVulnerabilities(iastJson, vulnerabilityType).ToList();
        vulnerabilities.Should().ContainSingle();
        return vulnerabilities[0];
    }

    internal static void AssertLocation(JObject vulnerability, string expectedPathContains, string expectedMethodContains)
    {
        var location = vulnerability["location"] as JObject;
        location.Should().NotBeNull();
        location!["path"]?.Value<string>().Should().Contain(expectedPathContains);
        location["method"]?.Value<string>().Should().Contain(expectedMethodContains);
    }

    internal static void AssertCookieEvidence(IEnumerable<JObject> vulnerabilities, string vulnerabilityType)
    {
        vulnerabilities.Where(v => string.Equals(v["type"]?.Value<string>(), vulnerabilityType, StringComparison.Ordinal))
                       .Select(v => v["evidence"]?["value"]?.Value<string>())
                       .Should()
                       .Contain("AllVulnerabilitiesCookieKey");
    }

    internal static void AssertSource(JObject iastJson, string origin, string name = null, string value = null)
    {
        var sources = iastJson["sources"] as JArray;
        sources.Should().NotBeNull();

        var source = sources!
                    .OfType<JObject>()
                    .FirstOrDefault(candidate =>
                         string.Equals(candidate["origin"]?.Value<string>(), origin, StringComparison.Ordinal)
                      && (name == null || string.Equals(candidate["name"]?.Value<string>(), name, StringComparison.Ordinal))
                      && (value == null || string.Equals(candidate["value"]?.Value<string>(), value, StringComparison.Ordinal)));

        source.Should().NotBeNull();
    }
}
#endif
