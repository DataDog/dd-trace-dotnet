// <copyright file="EvidenceRedactorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Http.QueryStringObfuscation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http
{
    [Collection(nameof(EvidenceRedactorTests))]
    public class EvidenceRedactorTests
    {
        private const double Timeout = 10_000;

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

        [SkippableFact]
        public void ObfuscateWithDefaultPattern()
        {
            var logger = new Mock<IDatadogLogger>();
            var obfuscatorRegex = TracerSettingsConstants.DefaultObfuscationQueryStringRegex;
            // the default regex seems to crash the regex engine on netcoreapp2.1 under arm64, with a null reference exception on the dotnet RegexRunner. Its ok as these arent supported in auto instrumentation, we just warn not to reuse this regex if 2.1&arm64 is the environment
#if NETCOREAPP2_1
            // Add old one otherwise NullReferenceException on arm64/netcoreapp2.1
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64 || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                obfuscatorRegex = """((?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:(?:\s|%20)*(?:=|%3D)[^&]+|(?:"|%22)(?:\s|%20)*(?::|%3A)(?:\s|%20)*(?:"|%22)(?:%2[^2]|%[^2]|[^"%])+(?:"|%22))|bearer(?:\s|%20)+[a-z0-9\._\-]|token(?::|%3A)[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L](?:[\w=-]|%3D)+\.ey[I-L](?:[\w=-]|%3D)+(?:\.(?:[\w.+\/=-]|%3D|%2F|%2B)+)?|[\-]{5}BEGIN(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY[\-]{5}[^\-]+[\-]{5}END(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY|ssh-rsa(?:\s|%20)*(?:[a-z0-9\/\.+]|%2F|%5C|%2B){100,})""";
            }
#endif
            var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, obfuscatorRegex, logger.Object);
            var queryString = "key1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2";
            var result = queryStringObfuscator.Obfuscate(queryString);
            result.Should().Be("key1=val1&<redacted>&key2=val2");
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
}
