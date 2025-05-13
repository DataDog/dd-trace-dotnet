// <copyright file="AspNetCore5IastAsm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.Security.IntegrationTests.Rcm;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class AspNetCore5IastAsm : RcmBase
{
    public AspNetCore5IastAsm(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5IastAsm))
    {
        EnableRasp(false);
    }

    [SkippableFact]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task TestRequestBodyTaintingSecurityEnabled()
    {
        EnableIast();
        DisableObfuscationQueryString();
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, "false");
        EnableIastTelemetry((int)IastMetricsVerbosityLevel.Off);

        var url = "/Iast/ExecuteQueryFromBodyQueryData";
        var body = "{\"Query\": \"SELECT Surname from Persons where name='Vicent'\"}";
        await TryStartApp();
        var agent = Fixture.Agent;
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        var spans1 = await SendRequestsAsync(agent, url, body, 1, 1, string.Empty, "application/json", null);
        var spans = new List<MockSpan>();
        spans.AddRange(spans1);
        await VerifySpans(spans.ToImmutableList(), settings);
    }

    protected override string GetTestName() => Prefix + nameof(AspNetCore5IastAsm);
}
#endif
