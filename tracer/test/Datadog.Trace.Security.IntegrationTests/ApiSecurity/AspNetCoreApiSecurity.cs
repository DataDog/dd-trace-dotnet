// <copyright file="AspNetCoreApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity;

public abstract class AspNetCoreApiSecurity : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private readonly AspNetCoreTestFixture _fixture;

    protected AspNetCoreApiSecurity(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableApiSecurity, string sampleName)
        : base(sampleName, outputHelper, "/shutdown", testName: $"ApiSecurity.{sampleName}.{(enableApiSecurity ? "ApiSecOn" : "ApiSecOff")}")
    {
        _fixture = fixture;
        _fixture.SetOutput(outputHelper);
        SetEnvironmentVariable(ConfigurationKeys.AppSec.Rules, DefaultRuleFile);
        Directory.CreateDirectory(LogDirectory);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, LogDirectory);
        if (enableApiSecurity)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled, "true");
            EnvironmentHelper.CustomEnvironmentVariables.Add(ConfigurationKeys.AppSec.ApiSecurityRequestSampleRate, "1");
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _fixture.SetOutput(null);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("/dataapi/model", """{"property":"dummy_rule", "property2":"test2", "property3": 2, "property4": 3}""", HttpStatusCode.Forbidden, true)]
    [InlineData("/dataapi/model", """{"property":"test", "property2":"test2", "property3": 2, "property4": 2}""", HttpStatusCode.OK, false)]
    public async Task TestApiSecurityScan(string url, string body, HttpStatusCode expectedStatusCode, bool containsAttack)
    {
        await TryStartApp();
        var agent = _fixture.Agent;
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body.Substring(0, 10), expectedStatusCode, containsAttack);
        var dateTime = DateTime.UtcNow;
        var result = await SubmitRequest(url, body, "application/json");
        var spans = agent.WaitForSpans(2, minDateTime: dateTime);
        await VerifySpans(spans, settings);
    }

    private async Task TryStartApp()
    {
        // we don't test with security off, but test with api security off, otherwise the matrix of tests would be too large
        await _fixture.TryStartApp(this, true);
        SetHttpPort(_fixture.HttpPort);
    }
}
#endif
