// <copyright file="AspNetWebApiAsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

[Collection("IisTests")]
public class AspNetWebApiAsmDataIntegratedWithSecurity : AspNetWebApiAsmData
{
    public AspNetWebApiAsmDataIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebApiAsmDataIntegratedWithoutSecurity : AspNetWebApiAsmData
{
    public AspNetWebApiAsmDataIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: false)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebApiAsmDataClassicWithSecurity : AspNetWebApiAsmData
{
    public AspNetWebApiAsmDataClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebApiAsmDataClassicWithoutSecurity : AspNetWebApiAsmData
{
    public AspNetWebApiAsmDataClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: false)
    {
    }
}

public abstract class AspNetWebApiAsmData : RcmBaseFramework, IClassFixture<IisFixture>, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly bool _classicMode;

    public AspNetWebApiAsmData(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("WebApi", output, "/api/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(enableSecurity);

        _classicMode = classicMode;
        _iisFixture = iisFixture;
        _testName = "Security." + nameof(AspNetWebApiAsmData)
                                + (classicMode ? ".Classic" : ".Integrated")
                                + ".enableSecurity=" + enableSecurity; // assume that arm is the same
    }

    [SkippableTheory]
    [InlineData("blocking-ips", "/api/health")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    public async Task TestBlockedRequestIp(string test, string url)
    {
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        // we want to see the ip here
        var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
        var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
        var spanBeforeAsmData = await SendRequestsAsync(_iisFixture.Agent, url);

        var productName = RcmProducts.AsmData;
        var id = nameof(TestBlockedRequestIp) + Guid.NewGuid();
        var id2 = nameof(TestBlockedRequestIp) + Guid.NewGuid();

        var response = _iisFixture.Agent.SetupRcm(
            Output,
            new[]
            {
                (
                    (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, productName, id),
                (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, productName, id2),
            });

        await _iisFixture.Agent.WaitRcmRequestAndReturnMatchingRequest(response);

        var spanAfterAsmData = await SendRequestsAsync(_iisFixture.Agent, url);
        var spans = new List<MockSpan>();
        spans.AddRange(spanBeforeAsmData);
        spans.AddRange(spanAfterAsmData);
        await VerifySpans(spans.ToImmutableList(), settings, true);
    }

    [SkippableTheory]
    [InlineData("blocking-user", "/api/user")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    public async Task TestBlockedRequestUser(string test, string url)
    {
        SetClientIp("90.91.8.235");
        try
        {
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(parameters: new object[] { test, sanitisedUrl });

            var spanBeforeAsmData = await SendRequestsAsync(_iisFixture.Agent, url);
            var productName = RcmProducts.AsmData;
            var response = _iisFixture.Agent.SetupRcm(
                Output,
                new[]
                {
                    ((object)new Payload
                     {
                         RulesData = new[] { new RuleData { Id = "blocked_users", Type = "data_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = "user3" } } } }
                     },
                     productName, nameof(TestBlockedRequestUser))
                });

            await _iisFixture.Agent.WaitRcmRequestAndReturnMatchingRequest(response);

            var spanAfterAsmData = await SendRequestsAsync(_iisFixture.Agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }
        finally
        {
            SetClientIp(MainIp);
        }
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
