// <copyright file="AspNetCore5AsmAttackerBlocking.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

public class AspNetCore5AsmAttackerBlocking : RcmBase
{
    private const string AsmProduct = "ASM";

    public AspNetCore5AsmAttackerBlocking(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmAttackerBlocking))
    {
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        SetEnvironmentVariable("DD_APPSEC_WAF_DEBUG", "0");
    }

    [Fact]
    [Trait("RunOnWindows", "True")]
    public async Task TestSuspiciousAttackerBlocking()
    {
        List<KeyValuePair<string, string>> headersAttacker = new()
        {
            new KeyValuePair<string, string>("http.client_ip", "34.65.27.85"),
            new KeyValuePair<string, string>("X-Real-Ip", "34.65.27.85"),
            new KeyValuePair<string, string>("accept-encoding", "identity"),
            new KeyValuePair<string, string>("x-forwarded-for", null),
        };

        List<KeyValuePair<string, string>> headersRegular = new()
        {
            new KeyValuePair<string, string>("X-Real-Ip", null),
            new KeyValuePair<string, string>("accept-encoding", "identity"),
            new KeyValuePair<string, string>("x-forwarded-for", null),
        };

        var headersAttackerArachni = new List<KeyValuePair<string, string>>(headersAttacker)
        {
            new KeyValuePair<string, string>("User-Agent", "Arachni/v1"),
        };

        var headersRegularArachni = new List<KeyValuePair<string, string>>(headersRegular)
        {
            new KeyValuePair<string, string>("User-Agent", "Arachni/v1"),
        };

        var headersAttackerScanner = new List<KeyValuePair<string, string>>(headersAttacker)
        {
            new KeyValuePair<string, string>("User-Agent", "dd-test-scanner-log-block"),
        };

        var headersRegularScanner = new List<KeyValuePair<string, string>>(headersRegular)
        {
            new KeyValuePair<string, string>("User-Agent", "dd-test-scanner-log-block"),
        };

        string url = "/Health";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var result = SubmitRequest(url, null, null, headers: headersAttackerScanner);
        result.Result.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var configurationInitial = new[]
        {
            ((object)new AppSec.Rcm.Models.Asm.Payload
            {
                Actions = new[]
                {
                    new Action { Id = "block", Type = BlockingAction.BlockRequestType, Parameters = new AppSec.Rcm.Models.Asm.Parameter { StatusCode = 403, Type = "json" } }
                },
            },
            AsmProduct,
            nameof(TestSuspiciousAttackerBlocking)),
            ((object)new AsmFeatures
            {
                Asm = new AsmFeature { Enabled = true },
            },
            "ASM_FEATURES",
            nameof(TestSuspiciousAttackerBlocking))
        };

        await agent.SetupRcmAndWait(Output, configurationInitial);
        result = SubmitRequest(url, null, null, headers: headersAttackerScanner);

        var exclusions = "[{\"id\": \"exc-000-001\",\"on_match\": \"block_custom\",\"conditions\": [{\"operator\": \"ip_match\",\"parameters\": {\"data\": \"suspicious_ips_data_id\", \"inputs\": [{\"address\": \"http.client_ip\"}]}}]}]";
        var configuration = new[]
        {
            (new AppSec.Rcm.Models.Asm.Payload
            {
                Actions = new[]
                {
                    new Action { Id = "block_custom", Type = BlockingAction.BlockRequestType, Parameters = new AppSec.Rcm.Models.Asm.Parameter { StatusCode = 405, Type = "auto" } }
                },
                Exclusions = (JArray)JToken.Parse(exclusions)
            },
            AsmProduct,
            nameof(TestSuspiciousAttackerBlocking)),
            ((object)new AppSec.Rcm.Models.AsmData.Payload
            {
                ExclusionsData = new[]
                {
                    new AppSec.Rcm.Models.AsmData.RuleData { Id = "suspicious_ips_data_id", Type = "ip_with_expiration", Data = new AppSec.Rcm.Models.AsmData.Data[] { new() { Value = "34.65.27.85" } } }
                },
            },
            "ASM_DATA",
            nameof(TestSuspiciousAttackerBlocking)),
        };

        await agent.SetupRcmAndWait(Output, configuration);
        result = SubmitRequest(url + "?a=3", null, null, headers: headersAttackerScanner);
        result.Result.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        result = SubmitRequest(url + "?a=4", null, null, headers: headersRegularScanner);
        result.Result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result = SubmitRequest(url + "?a=5", null, null, headers: headersAttackerArachni);
        result.Result.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        result = SubmitRequest(url + "?a=6", null, null, headers: headersRegularArachni);
        result.Result.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
#endif
