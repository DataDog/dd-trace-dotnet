// <copyright file="QueryStringObfuscatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Http.QueryStringObfuscation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http;

[Collection(nameof(QueryStringObfuscatorTests))]
public class QueryStringObfuscatorTests
{
    // seems on macos, netcore<=3.0 it can timeout
    private const double Timeout = 20000;

    public static IEnumerable<object[]> GetData()
    {
        var allData = new List<(string Data, string Expected)>
        {
            new(
                "http://google.fr/waf?key1=val1&key2=val2&key3=val3",
                "http://google.fr/waf?key1=val1&key2=val2&key3=val3"),
            new(
                "http://google.fr/waf?pass=03cb9f67-dbbc-4cb8-b966-329951e10934&key2=val2&key3=val3",
                "http://google.fr/waf?<redacted>&key2=val2&key3=val3"),
            // same as above, but with different case to test case-insensitivity
            new(
                "http://google.fr/waf?PASS=03cb9f67-dbbc-4cb8-b966-329951e10934&key2=val2&key3=val3",
                "http://google.fr/waf?<redacted>&key2=val2&key3=val3"),
            new(
                "http://google.fr/waf?key1=val1&public_key=MDNjYjlmNjctZGJiYy00Y2I4LWI5NjYtMzI5OTUxZTEwOTM0&key3=val3",
                "http://google.fr/waf?key1=val1&<redacted>&key3=val3"),
            new(
                "http://google.fr/waf?key1=val1&key2=val2&token=03cb9f67dbbc4cb8b966329951e10934",
                "http://google.fr/waf?key1=val1&key2=val2&<redacted>"),
            new(
                "http://google.fr/waf?json=%7B%20%22sign%22%3A%20%22%7B0x03cb9f67%2C0xdbbc%2C0x4cb8%2C%7B0xb9%2C0x66%2C0x32%2C0x99%2C0x51%2C0xe1%2C0x09%2C0x34%7D%7D%22%7D",
                "http://google.fr/waf?json=%7B%20<redacted>%7D"),
            new(
                "https://google.fr/waf?token=03cb9f67dbbc4cb8b9&key1=val1&key2=val2&pass=03cb9f67-dbbc-4cb8-b966-329951e10934&public_key=MDNjYjlmNjctZGJiYy00Y2I4LWI5NjYtMzI5OTUxZTEwOTM0&key3=val3&json=%7B%20%22sign%22%3A%20%22%7D%7D%22%7D",
                "https://google.fr/waf?<redacted>&key1=val1&key2=val2&<redacted>&<redacted>&key3=val3&json=%7B%20<redacted>%7D"),
            new(
                "http://google.fr/waf?password=12345&token=token:1234&bearer 1234&ecdsa-1-1 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa= test&old-pwd2=test&ssh-dss aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa= test&application_key=test&app-key=test2",
                "http://google.fr/waf?<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>"),
            // same as above, but with different case to test case-insensitivity
            new(
                "http://google.fr/waf?PassWord=12345&Token=token:1234&Bearer 1234&ecdsa-1-1 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa= test&old-pwd2=test&ssh-dss aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa= test&application_key=test&app-key=test2",
                "http://google.fr/waf?<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>&<redacted>"),
        };
        return allData.Select(e => new[] { e.Data, e.Expected });
    }

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    public void ObfuscateWithDefaultPattern(string url, string expected)
    {
        // the default regex seems to crash the regex engine on netcoreapp2.1 under arm64, with a null reference exception on the dotnet RegexRunner. Its ok as these arent supported in auto instrumentation, we just warn not to reuse this regex if 2.1&arm64 is the environment
#if NETCOREAPP2_1
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
#endif
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);
        var result = queryStringObfuscator.Obfuscate(url);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DoesntObfuscateIfNoPattern(string pattern)
    {
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, pattern, logger.Object);
        var originalQueryString = "key1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2";
        var result = queryStringObfuscator.Obfuscate(originalQueryString);
        result.Should().Be(originalQueryString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EdgeCases(string querystring)
    {
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);
        var result = queryStringObfuscator.Obfuscate(querystring);
        result.Should().Be(querystring);
    }

    // Culture-sensitive case-insensitive matching mis-cases non-ASCII characters (e.g. the Turkish
    // dotted/dotless 'I'), which both produces a CPU spike when scanning non-ASCII query strings on
    // .NET Framework and, worse, causes keywords containing an 'i' (api, public, signature, ...) to
    // be missed entirely under cultures like tr-TR. The obfuscator must match culture-independently.
    [SkippableTheory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    [InlineData("en-US")]
    public void ObfuscatesIndependentlyOfCurrentCulture(string culture)
    {
        // the default regex seems to crash the regex engine on netcoreapp2.1 under arm64, with a null reference exception on the dotnet RegexRunner. Its ok as these arent supported in auto instrumentation, we just warn not to reuse this regex if 2.1&arm64 is the environment
#if NETCOREAPP2_1
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
#endif
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);

            var logger = new Mock<IDatadogLogger>();
            // Constructed under the test culture so the underlying Regex captures it.
            var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);

            // Upper-case keywords containing 'I' only match when 'I' folds to 'i', which is false
            // under Turkic cultures unless the regex is culture-invariant.
            var result = queryStringObfuscator.Obfuscate("http://google.fr/waf?API_KEY=secret123&SIGNATURE=abc&PUBLIC_KEY=zzz&key=val");
            result.Should().Be("http://google.fr/waf?<redacted>&<redacted>&<redacted>&key=val");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // Same culture-folding root cause, exercised through a customer-style custom pattern over a
    // query string carrying non-ASCII (Korean) secret values. Under a Turkic culture the upper-case
    // keys containing 'I' (TICKET, EMPID) only match when 'I' folds to 'i', so without culture
    // invariance their Korean secret values leak unredacted. (The pattern itself is linear - this is
    // a correctness, not a backtracking, problem.)
    [Fact]
    public void ObfuscatesNonAsciiValuesWithCustomPatternIndependentlyOfCulture()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");

            var logger = new Mock<IDatadogLogger>();
            var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, @"(?i)(?<![a-zA-Z])(?:ticket|user|empid)=[^&]+", logger.Object);

            var result = queryStringObfuscator.Obfuscate("TICKET=비밀번호&USER=관리자&EMPID=한국&x=공개");
            result.Should().Be("<redacted>&<redacted>&<redacted>&x=공개");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // Regression guard for a reported production CPU spike on Vault file-download URLs carrying
    // URL-encoded CJK filenames alongside auth-related parameters. URL-encoding makes the input pure
    // ASCII ("%EC%84%A4..."), and the default pattern's repetition groups are linear (their
    // alternatives are mutually exclusive by the second character), so this must NOT exhibit
    // catastrophic backtracking. A deliberately tight timeout means any future change that
    // reintroduces super-linear backtracking would time out (Obfuscate returns string.Empty) and
    // fail the trailing-secret assertion below.
    [SkippableTheory]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(2000)]
    public void DefaultPatternDoesNotBacktrackOnUrlEncodedCjk(int filenameRepetitions)
    {
        // the default regex seems to crash the regex engine on netcoreapp2.1 under arm64, with a null reference exception on the dotnet RegexRunner. Its ok as these arent supported in auto instrumentation, we just warn not to reuse this regex if 2.1&arm64 is the environment
#if NETCOREAPP2_1
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
#endif
        const double tightTimeoutMs = 2000;
        var encodedCjkFilename = string.Concat(Enumerable.Repeat("%EC%84%A4%EA%B3%84%EC%9E%90%EB%A3%8C", filenameRepetitions));
        // Mirrors the reported URL shape, with a genuinely redactable secret appended so a successful
        // (non-timed-out) run is observable in the output.
        var queryString = $"/Vault/vaultserver.aspx?fileName={encodedCjkFilename}_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&password=hunter2";

        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(tightTimeoutMs, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);

        var result = queryStringObfuscator.Obfuscate(queryString);

        // Completed within the timeout (no RegexMatchTimeoutException, which would yield string.Empty)
        // and still redacted the trailing secret while leaving the encoded CJK bytes untouched.
        result.Should().Be($"/Vault/vaultserver.aspx?fileName={encodedCjkFilename}_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&<redacted>");
    }

    // The reported Vault "ticket" parameter is not actually matched by the default pattern (there is
    // no "ticket" keyword and the value does not satisfy any keyword suffix), so the URL passes
    // through unchanged - and, crucially, quickly.
    [SkippableFact]
    public void DefaultPatternPassesThroughUnmatchedVaultUrl()
    {
        // the default regex seems to crash the regex engine on netcoreapp2.1 under arm64, with a null reference exception on the dotnet RegexRunner. Its ok as these arent supported in auto instrumentation, we just warn not to reuse this regex if 2.1&arm64 is the environment
#if NETCOREAPP2_1
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
#endif
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);

        var url = "/Vault/vaultserver.aspx?fileName=%EC%84%A4%EA%B3%84%EC%9E%90%EB%A3%8C_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&ticket=VAULT_TOKEN";
        var result = queryStringObfuscator.Obfuscate(url);

        result.Should().Be(url);
    }

    [Fact]
    public void ObfuscateWithCustomPattern()
    {
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|authentic\d*)(?:\s*=[^&]+|""\s*:\s*""[^""]+"")|[a-z0-9\._\-]{100,}", logger.Object);
        var queryString = "?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&authentic3=val2&authentic55=v";
        var result = queryStringObfuscator.Obfuscate(queryString);
        result.Should().Be("?<redacted>&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&<redacted>&<redacted>");
    }
}
