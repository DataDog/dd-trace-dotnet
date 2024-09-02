// <copyright file="FingerprintTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class FingerprintTests : WafLibraryRequiredTest
{
    [Fact]
    public void GivenAFingerprintRequest_WhenRunWAF_FingerprintIsGenerated()
    {
        string ruleFile = "rasp-rule-set.json";
        var context = InitWaf(true, ruleFile, new Dictionary<string, object>(), out var waf);

        var args = new Dictionary<string, object>
        {
            { AddressesConstants.WafContextProcessor, new Dictionary<string, object> { { "fingerprint", true } } },
            { AddressesConstants.RequestUriRaw, "/path/to/resource/?key=" },
            { AddressesConstants.RequestMethod, "PUT" },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "key", "value" } } },
            { AddressesConstants.RequestBody, new Dictionary<string, string> { { "key", "value" } } },
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "user-agent", "Random" }, { "x-forwarded-for", "::1" } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "name", "albert" }, { "language", "en-GB" }, { "session_id", "ansd0182u2n" } } },
            { AddressesConstants.UserId, "admin" },
            { AddressesConstants.UserSessionId, "ansd0182u2n" }
        };
        var result = context.Run(args, TimeoutMicroSeconds);
        result.FingerprintDerivatives.Count.Should().Be(4);
    }

    [Fact]
    public void GivenAFingerprintRequest_WhenRunWAFWithEphemeral_FingerprintIsGenerated()
    {
        string ruleFile = "rasp-rule-set.json";
        var context = InitWaf(true, ruleFile, new Dictionary<string, object>(), out var waf);

        var args = new Dictionary<string, object>
        {
            { AddressesConstants.RequestUriRaw, "/path/to/resource/?key=" },
            { AddressesConstants.RequestMethod, "PUT" },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "key", "value" } } },
            { AddressesConstants.RequestBody, new Dictionary<string, string> { { "key", "value" } } },
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "user-agent", "Random" }, { "x-forwarded-for", "::1" } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "name", "albert" }, { "language", "en-GB" }, { "session_id", "ansd0182u2n" } } },
            { AddressesConstants.UserId, "admin" },
            { AddressesConstants.UserSessionId, "ansd0182u2n" }
        };
        context.Run(args, TimeoutMicroSeconds);
        args = new Dictionary<string, object>
        {
            { AddressesConstants.WafContextProcessor, new Dictionary<string, object> { { "fingerprint", true } } },
        };
        var resultEph = context.RunWithEphemeral(args, TimeoutMicroSeconds, true);
        resultEph.FingerprintDerivatives.Count.Should().Be(4);
    }

    private IContext InitWaf(bool newEncoder, string ruleFile, Dictionary<string, object> args, out Waf waf)
    {
        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: newEncoder,
            embeddedRulesetPath: ruleFile);
        waf = initResult.Waf;
        waf.Should().NotBeNull();
        var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        return context;
    }
}
