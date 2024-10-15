// <copyright file="FingerprintTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
    private static List<List<Dictionary<string, object>>> sampleData = new()
        {
            new List<Dictionary<string, object>>()
            {
                new Dictionary<string, object>
                {
                    { AddressesConstants.RequestUriRaw, "/path/to/resource/?key=" },
                    { AddressesConstants.RequestMethod, "PUT" },
                    { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "key", "value" } } },
                    { AddressesConstants.RequestBody, new Dictionary<string, string> { { "key", "value" } } },
                    { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "user-agent", "Arachni/v1.5.1" }, { "x-forwarded-for", "::1" }, { "custom", "value" } } },
                    { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "name", "albert" }, { "language", "en-GB" }, { "session_id", "ansd0182u2n" } } },
                    { AddressesConstants.UserId, "admin" },
                    { AddressesConstants.UserSessionId, "ansd0182u2n" }
                }
            },
            new List<Dictionary<string, object>>()
            {
                new Dictionary<string, object>()
                {
                    { "server.request.method", "GET" },
                    { "server.response.status", "200" },
                    { "server.request.uri.raw", "/Iast/GetFileContent?file=/nonexisting.txt" },
                    { "http.client_ip", "::1" },
                    { "server.request.query", new Dictionary<string, string[]> { { "file", new[] { "/nonexisting.txt" } } } },
                    {
                        "server.request.headers.no_cookies", new Dictionary<string, string[]>
                        {
                            { "cache-control", new[] { "max-age=0" } },
                            { "connection", new[] { "keep-alive" } },
                            { "accept", new[] { "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" } },
                            { "accept-encoding", new[] { "gzip , deflate, br, zstd" } },
                            { "accept-language", new[] { "en,es-ES;q=0.9,es;q=0.8" } },
                            { "host", new[] { "localhost:5003" } },
                            { "referer", new[] { "http://localhost:5003/" } },
                            { "user-agent", new[] { "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36" } },
                            { "sec-ch-ua", new[] { "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Google Chrome\";v=\"128\"" } },
                            { "sec-ch-ua-mobile", new[] { "?0" } },
                            { "sec-ch-ua-platform", new[] { "\"Windows\"" } },
                            { "upgrade-insecure-requests", new[] { "1" } },
                            { "sec-fetch-site", new[] { "same-origin" } },
                            { "sec-fetch-mode", new[] { "navigate" } },
                            { "sec-fetch-user", new[] { "?1" } },
                            { "sec-fetch-dest", new[] { "document" } }
                        }
                    },
                    {
                        "server.request.cookies", new Dictionary<string, string[]>
                        {
                            { ".AspNetCore.Antiforgery.dInqUVwbgyA", new[] { "CfDJ8DWtaLKARU1BicbVG7iD6w0lxol0VHahrPFrsql7ySzRtc-oxacIBn4biwlminJYcnuU_8hDW60aVfqux98gC6T-ZpAVQ7YK_fdPn6wbqLO4pNe9jQ3jwbhpMqgbl1Ng2nWoam8UexOt4Q0mP8lkADY" } },
                            { "__AntiXsrfToken", new[] { "6587af0f57a2452185675cb0c830f8ec" } },
                            { "accountType", new[] { "Personal" } },
                            { "file", new[] { "value1" } },
                            { "argumentLine", new[] { "value2" } },
                            { "SecureKey", new[] { "SecureValue" } },
                            { "SameSiteKey", new[] { "SameSiteValue" } },
                            { "AllVulnerabilitiesCookieKey", new[] { "AllVulnerabilitiesCookieValue&SameSite=None" } },
                            { "NoSameSiteKeyDefault", new[] { "NoSameSiteValueDefault" } },
                            { "NoSameSiteKeyNone", new[] { "NoSameSiteValueNOne&SameSite=None" } },
                            { "NoSameSiteKey", new[] { "NoSameSiteValue" } },
                            { "NoSameSiteKeyLax", new[] { "NoSameSiteValueLax" } },
                            { "NoSameSiteKeyDef", new[] { "NoSameSiteValueDef" } },
                            { "insecureKey", new[] { "insecureValue" } },
                            { ".AspNetCore.Correlation.oidc.xxxxxxxxxxxxxxxxxxx", new[] { "ExcludedCookieVulnValue" } },
                            { "NoHttpOnlyKey", new[] { "NoHttpOnlyValue" } },
                            { ".AspNetCore.Antiforgery.PEOLKb2zqCM", new[] { "CfDJ8N8HgaU-AuxItWDrE6uHmv5Nx2auCU2Nz4wPtc65npv5lxXup6TheS5Af5OxeI4p-F8qu4m3bqB9ngwMMNLTksq3J4rDOfDleYwS0ZO6sus4UQwJj9PXsX_tmEjSjvyHBQSyj0erEIC206XNFDz7oi8" } },
                            { ".AspNetCore.Antiforgery.dv9Y7hx5lOY", new[] { "CfDJ8N8HgaU-AuxItWDrE6uHmv5vbQjBTneuzffR0UWgwEbiUtb6cQDjp8VEQ9JVGvqF_BW0M85B3spAuysW_rY5tFQ6R-pNAnwJnAvoO-zwxA7Fx6mepz2G7BFXjnEpeHkew7ARSyoryR12nYpD1tNU9S8" } },
                            { ".AspNetCore.Antiforgery.jE_SavyZJRU", new[] { "CfDJ8N8HgaU-AuxItWDrE6uHmv7NfxQhx1lE8BOs9z5BaqShFHv9a4xh3pg5gA40Yqo3uy4gFtk_oTW_aPSfpppytPKAdb-oYYt6FX5ajDN21C9yAIF3Ob8xBGGbBBoXKVXGE4g0AEan5HaSlUk2WxdY0PY" } },
                            { ".AspNetCore.Session", new[] { "CfDJ8L/VJqbgbZ9AmItcsJeWhzGYBGDf0hQBOKYF9PXBQb+iCK/LWVuiTTVjmvN2NiKjtG08fe/7iqy/UwEmjtIf4RSZbvaAtdx5IjYu8J3t51dfons7ldcMWlvVu0hFrlB0aPuIiu25BAD1A4CXFuIfDIp+U3C/eCaHU9EJNLdKDh3c" } },
                            { "umb_installId", new[] { "9a334af1-788e-473d-ae5c-ed64edc39f3a" } },
                            { "SafeCookieKey", new[] { "SafeCookieValue&SameSite=Strict" } },
                            { "UnsafeEmptyCookie", new[] { "SameSite=None" } }
                        }
                    }
                },
                new Dictionary<string, object>()
                {
                     { AddressesConstants.FileAccess, "/nonexisting.txt" }
                }
            },
            new List<Dictionary<string, object>>()
            {
                new Dictionary<string, object>()
                {
                    { AddressesConstants.RequestUriRaw, "/path/to/resource.php" },
                    { AddressesConstants.RequestMethod, "GET" },
                    { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "name", "datadog" }, { "index", "295" } } },
                    { AddressesConstants.RequestBody, new Dictionary<string, string>() },
                    { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, object> { { "user-agent", new string[] { "Arachni/v1.5.1" } }, { "x-forwarded-for", "1.2.3.4" }, { "Accept-Language", "en_UK" }, { "X-Custom-Header", "42" } } },
                    { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "Expires ", "tomorrow" }, { "SessionToken", "12asd8ahjsd91j289" } } },
                    { AddressesConstants.UserId, "non-admin" }
                }
            }
        };

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    public void GivenAFingerprintRequest_WhenRunWAF_FingerprintIsGenerated(int testIndex, int resultingHeaders)
    {
        var ruleFile = "rasp-rule-set.json";
        var context = InitWaf(true, ruleFile, new Dictionary<string, object>(), out var waf);
        List<Dictionary<string, object>> args;

        args = sampleData[testIndex];
        IResult result = null;
        foreach (var data in args)
        {
            result = context.Run(data, TimeoutMicroSeconds);
            result.Timeout.Should().BeFalse("Timeout should be false");
        }

        result.FingerprintDerivatives.Count.Should().Be(resultingHeaders);
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
