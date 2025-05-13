// <copyright file="AspNetFxWebApiApiSecurity.cs" company="Datadog">
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
public class AspNetFxWebApiApiSecurityEnabled(IisFixture iisFixture, ITestOutputHelper output) : AspNetFxWebApiApiSecurity(iisFixture, output, enableApiSecurity: true);

[Collection("IisTests")]
public class AspNetFxWebApiApiSecurityDisabled(IisFixture iisFixture, ITestOutputHelper output) : AspNetFxWebApiApiSecurity(iisFixture, output, enableApiSecurity: false);

public abstract class AspNetFxWebApiApiSecurity : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;

    internal AspNetFxWebApiApiSecurity(IisFixture iisFixture, ITestOutputHelper output, bool enableApiSecurity)
        : base("WebApi", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(true);
        EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.Rules, "ApiSecurity\\ruleset-with-block.json");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityEnabled, enableApiSecurity.ToString());

        _iisFixture = iisFixture;
        AddCookies(new Dictionary<string, string> { { "cookie-key", "cookie-value" } });
        _testName = "Security." + nameof(AspNetFxWebApiApiSecurity)
                                + ".enableApiSecurity=" + enableApiSecurity;
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("LoadFromGAC", "True")]
    [InlineData("scan-without-attack", "/api/home/api-security/12", """{"Dog":"23", "Dog2":"test", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    [InlineData("scan-with-attack", "/api/home/api-security/12", """{"Dog":"23", "Dog2":"dev/zero", "Dog3": 2.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    [InlineData("scan-empty-model", "/api/home/empty-model", """{"Dog":"23", "Dog2":"test", "Dog3": 1.5, "Dog4": 1.6, "NonExistingProp" : 1}""")]
    public async Task TestApiSecurityScan(string scenario, string url, string body)
    {
        var agent = _iisFixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseTextForParameters($"scenario={scenario}");
        var dateTime = DateTime.UtcNow;
        var res = await SubmitRequest(url, body, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        await VerifySpans(spans, settings);
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
        // we need to have a first request to the home page to avoid the initialization metrics of the waf and sampling priority set to 2.0 because of that
        var answer = await SubmitRequest("/", null, string.Empty);
        // because of this we need to add a filter
        _iisFixture.Agent.SpanFilters.Add(s => !s.Resource.Contains("landing"));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override string GetTestName() => _testName;
}
#endif
