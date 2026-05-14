// <copyright file="AspNetWebFormsWithIast.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
namespace Datadog.Trace.Security.IntegrationTests.Iast;

[Collection("IisTests")]
public class AspNetWebFormsIntegratedWithIast : AspNetWebFormsWithIast
{
    public AspNetWebFormsIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsClassicIntegratedWithIast : AspNetWebFormsWithIast
{
    public AspNetWebFormsClassicIntegratedWithIast(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

public abstract class AspNetWebFormsWithIast : AspNetBase, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _classicMode;

    public AspNetWebFormsWithIast(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet")
    {
        EnableEvidenceRedaction(false);
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_REQUEST_SAMPLING", "100");
        SetEnvironmentVariable("DD_IAST_MAX_CONCURRENT_REQUESTS", "100");
        SetEnvironmentVariable("DD_IAST_VULNERABILITIES_PER_REQUEST", "100");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.StackTraceEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, VulnerabilityLogPath);

        _iisFixture = iisFixture;
        _classicMode = classicMode;
        _testName = "Security.AspNetWebForms"
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableSecurity=" + enableSecurity;
    }

    protected string VulnerabilityLogPath =>
        Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("/print?Encrypt=True&ClientDatabase=774E4D65564946426A53694E48756B592B444A6C43673D3D&p=413&ID=2376&EntityType=114&Print=True&OutputType=WORDOPENXML&SSRSReportID=1")]
    public async Task TestQueryParameterNameVulnerability(string url)
    {
        var since = DateTime.UtcNow;
        await SendRequestsAsync(url);
        var file = $"Iast.QueryParameterName.{(_classicMode ? "classic" : string.Empty)}.AspNetWebForms";
        await VulnerabilityJsonl.VerifyRecordsAsync(VulnerabilityLogPath, file, since: since);
    }

    public async Task InitializeAsync()
    {
        await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(_iisFixture.HttpPort);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override string GetTestName() => _testName;
}
#endif
