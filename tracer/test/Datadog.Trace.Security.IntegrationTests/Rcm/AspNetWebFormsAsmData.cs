// <copyright file="AspNetWebFormsAsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
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
public class AspNetWebFormsAsmDataIntegratedWithSecurity : AspNetWebFormsAsmData
{
    public AspNetWebFormsAsmDataIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsAsmDataIntegratedWithoutSecurity : AspNetWebFormsAsmData
{
    public AspNetWebFormsAsmDataIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: false, enableSecurity: false)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsAsmDataClassicWithSecurity : AspNetWebFormsAsmData
{
    public AspNetWebFormsAsmDataClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetWebFormsAsmDataClassicWithoutSecurity : AspNetWebFormsAsmData
{
    public AspNetWebFormsAsmDataClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
        : base(iisFixture, output, classicMode: true, enableSecurity: false)
    {
    }
}

public abstract class AspNetWebFormsAsmData : RcmBase, IClassFixture<IisFixture>
{
    private readonly IisFixture _iisFixture;
    private readonly bool _enableSecurity;
    private readonly string _testName;

    public AspNetWebFormsAsmData(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
        : base("WebForms", output, "/home/shutdown", @"test\test-applications\security\aspnet", testName: nameof(AspNetWebFormsAsmData))
    {
        SetSecurity(enableSecurity);
        SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Rules, DefaultRuleFile);

        _iisFixture = iisFixture;
        _enableSecurity = enableSecurity;
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        _testName = "Security." + nameof(AspNetWebFormsAsmData)
                 + (classicMode ? ".Classic" : ".Integrated")
                 + ".enableSecurity=" + enableSecurity;
        SetHttpPort(iisFixture.HttpPort);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [Theory]
    [InlineData("/Health?test&[$slice]", null)]
    [InlineData("/Health/Params/appscan_fingerprint", null)]
    [InlineData("/Health/wp-config", null)]
    [InlineData("/Health?arg=[$slice]", null)]
    [InlineData("/Health", "ctl00%24MainContent%24testBox=%5B%24slice%5D")]
    public Task TestSecurity(string url, string body)
    {
        // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
        // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedUrl, body);
        return TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, body, 5, 1, settings, "application/x-www-form-urlencoded");
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [Trait("LoadFromGAC", "True")]
    [SkippableTheory]
    [InlineData("blocking")]
    public async Task TestBlockedRequest(string test)
    {
        var url = "/Health";

        var settings = VerifyHelper.GetSpanVerifierSettings(test);
        await TestAppSecRequestWithVerifyAsync(_iisFixture.Agent, url, null, 5, SecurityEnabled ? 1 : 2, settings, userAgent: "Hello/V");
    }

    protected override string GetTestName() => _testName;
}
#endif
