// <copyright file="HashTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class HashTests
{
    [Theory]
    [InlineData(".", 1415119778)]
    [InlineData("_", -905593483)]
    [InlineData("0", 356220456)]
    [InlineData("1", 1974254443)]
    [InlineData("2", -702678866)]
    [InlineData("a", -1964492805)]
    [InlineData("A", 2092994459)]
    [InlineData("b", -346458818)]
    [InlineData("B", -583938850)]
    [InlineData("c", 1271575169)]
    [InlineData("C", 1034095137)]
    [InlineData(VulnerabilityTypeName.CommandInjection, -969366626)]
    [InlineData(VulnerabilityTypeName.HardcodedSecret, 900913240)]
    [InlineData(VulnerabilityTypeName.HeaderInjection, -283297663)]
    [InlineData(VulnerabilityTypeName.HstsHeaderMissing, 1456880170)]
    [InlineData(VulnerabilityTypeName.InsecureCookie, 598480805)]
    [InlineData(VulnerabilityTypeName.LdapInjection, 1959242366)]
    [InlineData(VulnerabilityTypeName.NoHttpOnlyCookie, -853801519)]
    [InlineData(VulnerabilityTypeName.NoSameSiteCookie, -386409364)]
    [InlineData(VulnerabilityTypeName.NoSqlMongoDbInjection, -356000854)]
    [InlineData(VulnerabilityTypeName.PathTraversal, 1047354834)]
    [InlineData(VulnerabilityTypeName.SqlInjection, -1258592580)]
    [InlineData(VulnerabilityTypeName.Ssrf, -972587577)]
    [InlineData(VulnerabilityTypeName.StackTraceLeak, 968833153)]
    [InlineData(VulnerabilityTypeName.TrustBoundaryViolation, -633406949)]
    [InlineData(VulnerabilityTypeName.UnvalidatedRedirect, -1467920366)]
    [InlineData(VulnerabilityTypeName.WeakHash, 924640792)]
    [InlineData(VulnerabilityTypeName.WeakRandomness, -1681736197)]
    [InlineData(VulnerabilityTypeName.XContentTypeHeaderMissing, -1311908003)]
    [InlineData(VulnerabilityTypeName.XPathInjection, 1860579842)]
    [InlineData(0, 391)]
    [InlineData(1, 392)]
    [InlineData(10, 401)]
    [InlineData(100, 491)]
    [InlineData(2, 393)]
    [InlineData(20, 411)]
    [InlineData(42, 433)]
    [InlineData(523, 914)]
    public void GivenAKownString_WhenCalculatedHash_ValueIsEspected(object value, int hash)
    {
        Assert.Equal(hash, IastUtils.GetHashCode(value));
    }

    [Theory]
    [InlineData(VulnerabilityTypeName.WeakHash, "AspNetCoreRateLimit.RateLimitProcessor", "BuildCounterKey", 890383720)]
    [InlineData(VulnerabilityTypeName.Xss, "AspNetCore.Views_Iast_ReflectedXss+<<ExecuteAsync>b__8_1>d", "MoveNext", -1004380463)]
    public void GivenAKownVulnerability_WhenCalculatedHash_ValueIsExpected(string vulnName, string path, string method, int expectedHash)
    {
        var vulnerability = new Vulnerability(vulnName, new Location(path, method, null, 849303611103961300), new Evidence("Evidence"));
        var hashCode = vulnerability.GetHashCode();
        Assert.Equal(expectedHash, hashCode);
    }

    [Theory]
    [InlineData(VulnerabilityTypeName.InsecureCookie, "AspNetCoreRateLimit.RateLimitProcessor", false, -304624042)]
    [InlineData(VulnerabilityTypeName.InsecureCookie, "AspNetCoreRateLimit.RateLimitProcessor", true, 990913114)]
    [InlineData(VulnerabilityTypeName.InsecureCookie, "AspNetCore.Views_Iast_ReflectedXss+<<ExecuteAsync>b__8_1>d", true, 990913114)]
    [InlineData(VulnerabilityTypeName.NoSameSiteCookie, "AspNetCore.Views_Iast_ReflectedXss+<<ExecuteAsync>b__8_1>d", false, 1003850134)]
    [InlineData(VulnerabilityTypeName.NoSameSiteCookie, "AspNetCore.Views_Iast_ReflectedXss+<<ExecuteAsync>b__8_1>d", true, -636226626)]
    [InlineData(VulnerabilityTypeName.NoSameSiteCookie, "AspNetCoreRateLimit.RateLimitProcessor", true, -636226626)]
    public void GivenACookie_WhenCalculatedHash_ValueIsExpected(string vulnName, string cookieName, bool isFiltered, int hash)
    {
        IastModule.GetCookieHash(vulnName, cookieName, isFiltered).Should().Be(hash);
    }
}
