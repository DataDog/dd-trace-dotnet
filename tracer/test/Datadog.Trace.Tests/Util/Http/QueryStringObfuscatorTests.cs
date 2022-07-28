// <copyright file="QueryStringObfuscatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http.QueryStringObfuscation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http
{
    [Collection(nameof(QueryStringObfuscatorTests))]
    public class QueryStringObfuscatorTests
    {
        private const double Timeout = 500;

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
            var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettings.DefaultObfuscationQueryStringRegex, logger.Object);
            var result = queryStringObfuscator.Obfuscate(querystring);
            result.Should().Be(querystring);
        }

        [Fact]
        public void ObfuscateWithDefaultPattern()
        {
            var logger = new Mock<IDatadogLogger>();
            var queryStringObfuscator = ObfuscatorFactory.GetObfuscator(Timeout, TracerSettings.DefaultObfuscationQueryStringRegex, logger.Object);
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
