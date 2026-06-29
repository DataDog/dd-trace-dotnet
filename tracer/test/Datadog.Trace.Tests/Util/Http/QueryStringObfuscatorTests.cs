// <copyright file="QueryStringObfuscatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public void ObfuscateWithCustomPattern()
    {
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|authentic\d*)(?:\s*=[^&]+|""\s*:\s*""[^""]+"")|[a-z0-9\._\-]{100,}", logger.Object);
        var queryString = "?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&authentic3=val2&authentic55=v";
        var result = queryStringObfuscator.Obfuscate(queryString);
        result.Should().Be("?<redacted>&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2&<redacted>&<redacted>");
    }

    [Theory]
    [InlineData(
        // Vault file download URL with Korean filename + auth ticket — the pattern that caused
        // catastrophic backtracking with the default regex (CPU spike to 90% in production).
        // High-byte URL-encoded sequences (%EC, %84, %A4...) must be stripped before obfuscation
        // so that the regex engine does not attempt to match them against JWT/SSH/bearer patterns.
        "dbName=CPMS&fileId=F4D7D9C025CF4FB29023189592B261D1&fileName=%EC%84%A4%EA%B3%84%EC%9E%90%EB%A3%8C%EC%86%A1%EB%B6%80%EC%84%9C_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&token=VAULTTOKEN1234567",
        "dbName=CPMS&fileId=F4D7D9C025CF4FB29023189592B261D1&fileName=_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&<redacted>")]
    [InlineData(
        // Japanese filename — same high-byte URL encoding pattern
        "filename=%E6%97%A5%E6%9C%AC%E8%AA%9E%E3%83%86%E3%82%B9%E3%83%88.pdf&token=abc1234567890ab",
        "filename=.pdf&<redacted>")]
    [InlineData(
        // ASCII-only URL — high-byte stripping must NOT affect ASCII credential parameters
        "key1=val1&token=abc1234567890ab&key2=val2",
        "key1=val1&<redacted>&key2=val2")]
    [InlineData(
        // Mixed: ASCII params + Korean filename — only ticket should be redacted
        "database=CPMS&fileName=%EC%84%A4%EA%B3%84.xlsx&user=emma.bae",
        "database=CPMS&fileName=.xlsx&user=emma.bae")]
    public void ObfuscateWithDefaultPattern_MultibyteUrlEncoding(string queryString, string expected)
    {
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);
        var result = queryStringObfuscator.Obfuscate(queryString);
        result.Should().Be(expected);
    }

    [Fact]
    public void ObfuscateWithDefaultPattern_MultibyteUrlEncoding_CompletesWithinTimeout()
    {
        // Regression test: the default obfuscation regex must complete well within the timeout
        // when applied to a URL containing a long URL-encoded Korean filename + auth ticket.
        // Before the fix, RegexInterpreter.Go() consumed 16+ seconds/min on this pattern.
        var logger = new Mock<IDatadogLogger>();
        var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, logger.Object);

        var longKoreanFilename = string.Concat(Enumerable.Repeat("%EC%84%A4%EA%B3%84%EC%9E%90%EB%A3%8C", 10));
        var queryString = $"dbName=CPMS&fileId=ABC123&fileName={longKoreanFilename}_.xlsx&vaultId=67BBB9204FE84A8981ED8313049BA06C&token=VAULTTOKEN1234567";

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = queryStringObfuscator.Obfuscate(queryString);
        stopwatch.Stop();

        // Must not time out (return empty string) and must complete in well under 1 second
        result.Should().NotBeEmpty("obfuscation must not time out on multibyte URL-encoded filenames");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "catastrophic backtracking must not occur");
    }
}
