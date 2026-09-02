// <copyright file="WafAddressReuseTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

/// <summary>
/// The tracer now sends each request address to the WAF once, so a later run of the same request only
/// carries what changed. These tests pin what that costs: what the WAF still derives from the addresses
/// of an earlier run, and what genuinely has to be re-supplied.
/// </summary>
public class WafAddressReuseTests : WafLibraryRequiredTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenSchemaExtractionOnTheLastRun_WhenRequestAddressesAreNotResupplied_ThenSchemasAreStillProduced(bool resupplyRequestAddresses)
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        context.Run(RequestArgs(), TimeoutMicroSeconds).Should().NotBeNull();

        var lastArgs = resupplyRequestAddresses ? RequestArgs() : new Dictionary<string, object>();
        lastArgs[AddressesConstants.ResponseStatus] = "404";
        lastArgs[AddressesConstants.WafContextProcessor] = new Dictionary<string, bool> { { "extract-schema", true } };

        var result = context.Run(lastArgs, TimeoutMicroSeconds);

        result.Should().NotBeNull();
        result.ExtractSchemaDerivatives.Should().ContainKeys(
            "_dd.appsec.s.req.query",
            "_dd.appsec.s.req.headers",
            "_dd.appsec.s.req.cookies",
            "_dd.appsec.s.req.body");
    }

    /// <summary>
    /// ASP.NET only puts its session cookie in Request.Cookies once the session id has been read, which
    /// happens on the last WAF call of the request, so a request that arrived without cookies has none to
    /// send at the beginning and does have one to send at the end. Not re-reading them there is what
    /// empties the cookie halves of the session fingerprint.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenARequestWithoutCookies_WhenCookiesShowUpForTheLastRun_ThenOnlySendingThemFillsTheSessionFingerprint(bool sendLateCookies)
    {
        using var waf = CreateWaf().Waf;
        using var context = waf.CreateContext();

        var firstArgs = RequestArgs();
        firstArgs.Remove(AddressesConstants.RequestCookies);
        context.Run(firstArgs, TimeoutMicroSeconds).Should().NotBeNull();

        var lastArgs = new Dictionary<string, object> { { AddressesConstants.ResponseStatus, "404" }, { AddressesConstants.UserSessionId, "session-1234" } };
        if (sendLateCookies)
        {
            lastArgs[AddressesConstants.RequestCookies] = new Dictionary<string, string> { { "ASP.NET_SessionId", "session-1234" } };
        }

        var result = context.Run(lastArgs, TimeoutMicroSeconds);

        result.Should().NotBeNull();
        result.FingerprintDerivatives.Should().ContainKey("_dd.appsec.fp.session");

        // ssn-<user id hash>-<cookie fields hash>-<cookie values hash>-<session id hash>
        var parts = result.FingerprintDerivatives["_dd.appsec.fp.session"].ToString().Split('-');
        parts.Should().HaveCount(5);
        parts[4].Should().NotBeEmpty("the session id hash comes from usr.session_id");

        if (sendLateCookies)
        {
            parts[2].Should().NotBeEmpty("the cookie fields hash comes from server.request.cookies");
            parts[3].Should().NotBeEmpty("the cookie values hash comes from server.request.cookies");
        }
        else
        {
            parts[2].Should().BeEmpty();
            parts[3].Should().BeEmpty();
        }
    }

    private static Dictionary<string, object> RequestArgs() =>
        new()
        {
            { AddressesConstants.RequestMethod, "POST" },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/api/values" },
            { AddressesConstants.RequestClientIp, "10.0.0.1" },
            { AddressesConstants.RequestQuery, new Dictionary<string, string[]> { { "q", ["fun"] } } },
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "content-type", "application/json" } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "sid", "abc" } } },
            { AddressesConstants.RequestBody, new Dictionary<string, object> { { "property1", "value1" }, { "property2", 3 } } },
        };
}
