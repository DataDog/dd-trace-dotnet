// <copyright file="WafResponseStatusTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafResponseStatusTests : WafLibraryRequiredTest
{
    private const string ScannerRule = "nfd-000-001";
    private const string ScannerUrl = "http://localhost:54587/etc/passwd";

    [Fact]
    public void GivenAScannerRequest_WhenTheStatusIsSentOnlyAtResponseTime_ThenTheRuleMatches()
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        context.Run(RequestArgs(status: null), TimeoutMicroSeconds).Should().NotBeNull();

        MatchedRules(context.Run(ResponseArgs("404"), TimeoutMicroSeconds)).Should().Contain(ScannerRule);
    }

    [Fact]
    public void GivenAScannerRequest_WhenTheRequestPhaseFabricatesA200Status_ThenTheResponsePhaseStillMatches()
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        // re-supplying a persistent address replaces it and re-marks it as new, so 200 does not latch
        context.Run(RequestArgs(status: "200"), TimeoutMicroSeconds).Should().NotBeNull();

        MatchedRules(context.Run(ResponseArgs("404"), TimeoutMicroSeconds)).Should().Contain(ScannerRule);
    }

    [Fact]
    public void GivenAScannerRequest_WhenTheStatusIsSentTwice_ThenTheRuleMatchesOnce()
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        context.Run(RequestArgs(status: null), TimeoutMicroSeconds).Should().NotBeNull();
        MatchedRules(context.Run(new Dictionary<string, object> { { AddressesConstants.ResponseStatus, "404" } }, TimeoutMicroSeconds)).Should().Contain(ScannerRule);

        MatchedRules(context.Run(ResponseArgs("404"), TimeoutMicroSeconds)).Should().NotContain(ScannerRule);
    }

    [Fact]
    public void GivenAScannerRequest_WhenTheRealStatusIsNeverSent_ThenTheRuleNeverMatches()
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        MatchedRules(context.Run(RequestArgs(status: "200"), TimeoutMicroSeconds)).Should().NotContain(ScannerRule);
    }

    private static Dictionary<string, object> RequestArgs(string status)
    {
        var args = new Dictionary<string, object>
        {
            { AddressesConstants.RequestMethod, "GET" },
            { AddressesConstants.RequestUriRaw, ScannerUrl },
            { AddressesConstants.RequestClientIp, "10.0.0.1" },
        };

        if (status is not null)
        {
            args.Add(AddressesConstants.ResponseStatus, status);
        }

        return args;
    }

    private static Dictionary<string, object> ResponseArgs(string status) =>
        new()
        {
            { AddressesConstants.ResponseStatus, status },
            { AddressesConstants.ResponseHeaderNoCookies, new Dictionary<string, string[]> { { "content-type", ["text/html"] } } },
        };

    private static IEnumerable<string> MatchedRules(IResult result)
    {
        if (result?.Data is null)
        {
            return [];
        }

        var matches = JsonConvert.DeserializeObject<WafMatch[]>(JsonConvert.SerializeObject(result.Data));
        return matches.Select(m => m.Rule.Id).ToList();
    }
}
