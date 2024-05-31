// <copyright file="AspNetCore5StartupIastTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class AspNetCore5StartupIastTestsFullSamplingIastEnabled : AspNetCore5StartupIastTestsFullSampling
{
    public AspNetCore5StartupIastTestsFullSamplingIastEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, testName: "AspNetCore5StartupIastTestsEnabled")
    {
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestIastVerbTamperingVulnerability()
    {
        var filename = "Iast.VerbTampering.AspNetCore5.IastEnabled";
        var newFixture = new AspNetCoreTestFixture();
        newFixture.SetOutput(Output);

        var datetimeOffset = DateTimeOffset.UtcNow; // Catch vulnerability at the startup of the app
        await TryStartApp(newFixture);

        var agent = newFixture.Agent;
        var spans = agent.WaitForSpans(1, minDateTime: datetimeOffset);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        newFixture.Dispose();
        newFixture.SetOutput(null);
    }
}

[Collection(nameof(AspNetCore5StartupIastTestsFullSampling))]
[CollectionDefinition(nameof(AspNetCore5StartupIastTestsFullSampling), DisableParallelization = true)]
public abstract class AspNetCore5StartupIastTestsFullSampling : AspNetCore5StartupIastTests
{
    public AspNetCore5StartupIastTestsFullSampling(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? vulnerabilitiesPerRequest = null, bool redactionEnabled = false)
        : base(fixture, outputHelper, enableIast: enableIast, testName: testName, samplingRate: 100, isIastDeduplicationEnabled: isIastDeduplicationEnabled, vulnerabilitiesPerRequest: vulnerabilitiesPerRequest, redactionEnabled: redactionEnabled)
    {
    }
}

public abstract class AspNetCore5StartupIastTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5StartupIastTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableIast, string testName, bool? isIastDeduplicationEnabled = null, int? samplingRate = null, int? vulnerabilitiesPerRequest = null, bool? redactionEnabled = false, int iastTelemetryLevel = (int)IastMetricsVerbosityLevel.Off)
        : base("AspNetCore5Startup", outputHelper, "/shutdown", testName: testName)
    {
        Fixture = fixture;
        fixture.SetOutput(outputHelper);
        IastEnabled = enableIast;
        IsIastDeduplicationEnabled = isIastDeduplicationEnabled;
        VulnerabilitiesPerRequest = vulnerabilitiesPerRequest;
        SamplingRate = samplingRate;
        RedactionEnabled = redactionEnabled;
        IastTelemetryLevel = iastTelemetryLevel;
    }

    protected AspNetCoreTestFixture Fixture { get; }

    protected bool IastEnabled { get; }

    protected bool? RedactionEnabled { get; }

    protected bool? IsIastDeduplicationEnabled { get; }

    protected int? VulnerabilitiesPerRequest { get; }

    protected int? SamplingRate { get; }

    protected int IastTelemetryLevel { get; }

    public override void Dispose()
    {
        base.Dispose();
        Fixture.SetOutput(null);
    }

    public virtual async Task TryStartApp()
    {
        await TryStartApp(Fixture);
    }

    public virtual async Task TryStartApp(AspNetCoreTestFixture fixture)
    {
        EnableIast(IastEnabled);
        EnableEvidenceRedaction(RedactionEnabled);
        EnableIastTelemetry(IastTelemetryLevel);
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, IsIastDeduplicationEnabled?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, VulnerabilitiesPerRequest?.ToString() ?? string.Empty);
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, SamplingRate?.ToString() ?? string.Empty);
        await fixture.TryStartApp(this, enableSecurity: false);
        SetHttpPort(fixture.HttpPort);
    }
}

#endif
