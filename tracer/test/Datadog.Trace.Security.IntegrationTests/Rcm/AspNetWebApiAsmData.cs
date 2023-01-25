// <copyright file="AspNetWebApiAsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Configuration;
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

public abstract class AspNetWebApiAsmData : RcmBaseFramework, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

    public AspNetWebApiAsmData(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("WebApi", output, "/api/home/shutdown", @"test\test-applications\security\aspnet")
    {
        SetSecurity(enableSecurity);

        _iisFixture = iisFixture;
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetWebApiAsmData)
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableSecurity=" + enableSecurity; // assume that arm is the same
        SetHttpPort(iisFixture.HttpPort);
    }

    [SkippableTheory]
    [InlineData("blocking-ips", "/api/health")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    public async Task TestBlockedRequestIp(string test, string url)
    {
        HttpStatusCode expectedStatusCode = SecurityEnabled ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{_iisFixture.IisExpress.Process.ProcessName}*", LogDirectory);
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        // we want to see the ip here
        var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
        var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
        var spanBeforeAsmData = await SendRequestsAsync(_iisFixture.Agent, url);

        var product = new AsmDataProduct();
        _iisFixture.Agent.SetupRcm(
            Output,
            new[]
            {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
            },
            product.Name);

        var request1 = await _iisFixture.Agent.WaitRcmRequestAndReturnLast();
        if (SecurityEnabled)
        {
            await logEntryWatcher.WaitForLogEntry(RulesUpdatedMessage(_iisFixture.IisExpress.Process.Id), LogEntryWatcherTimeout);
        }
        else
        {
            await Task.Delay(1500);
        }

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
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{_iisFixture.IisExpress.Process.ProcessName}*", LogDirectory);
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(parameters: new object[] { test, sanitisedUrl });

            var spanBeforeAsmData = await SendRequestsAsync(_iisFixture.Agent, url);

            var product = new AsmDataProduct();
            _iisFixture.Agent.SetupRcm(
                Output,
                new[]
                {
                        ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_users", Type = "data_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = "user3" } } } } }, "asm_data")
                },
                product.Name);

            var request1 = await _iisFixture.Agent.WaitRcmRequestAndReturnLast();
            if (SecurityEnabled)
            {
                await logEntryWatcher.WaitForLogEntry(RulesUpdatedMessage(_iisFixture.IisExpress.Process.Id), LogEntryWatcherTimeout);
            }
            else
            {
                await Task.Delay(1500);
            }

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

    protected override string GetTestName() => _testName;
}
#endif
