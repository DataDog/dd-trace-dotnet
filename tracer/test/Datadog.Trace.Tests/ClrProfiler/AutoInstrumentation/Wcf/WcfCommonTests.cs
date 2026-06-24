// <copyright file="WcfCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Wcf
{
    public class WcfCommonTests
    {
        [Fact]
        public void BuildHttpUrl_ObfuscatesSensitiveQueryStringValues()
        {
            var uri = new Uri("http://service/op?token=secret&api_key=abc");

            var result = WcfCommon.BuildHttpUrl(uri, DefaultManager());

            result.Should().NotBeNull();
            result.Should().Contain("<redacted>");
            result.Should().NotContain("secret");
            result.Should().NotContain("abc");
        }

        [Fact]
        public void BuildHttpUrl_StripsUserInfo()
        {
            var uri = new Uri("http://user:pass@service/op");

            var result = WcfCommon.BuildHttpUrl(uri, DefaultManager());

            result.Should().NotBeNull();
            result.Should().NotContain("user");
            result.Should().NotContain("pass");
        }

        [Fact]
        public void BuildHttpUrl_PreservesNonSensitiveQueryString()
        {
            var uri = new Uri("http://service/op?id=123&sort=asc");

            var result = WcfCommon.BuildHttpUrl(uri, DefaultManager());

            result.Should().Be("http://service/op?id=123&sort=asc");
        }

        [Fact]
        public void BuildHttpUrl_NullUri_ReturnsNull()
        {
            var result = WcfCommon.BuildHttpUrl(null, DefaultManager());

            result.Should().BeNull();
        }

        [Theory]
        [InlineData("net.tcp://localhost:8585/Sample/Service", "net.tcp://localhost:8585/Sample/Service")]
        [InlineData("net.pipe://localhost/sample", "net.pipe://localhost/sample")]
        [InlineData("net.msmq://machine/private/queue", "net.msmq://machine/private/queue")]
        public void BuildHttpUrl_NonHttpHierarchicalScheme_PreservesAbsoluteUri(string input, string expected)
        {
            var result = WcfCommon.BuildHttpUrl(new Uri(input), DefaultManager());

            result.Should().Be(expected);
        }

        [Fact]
        public void BuildHttpUrl_OpaqueScheme_PreservesAbsoluteUri()
        {
            // Regression: MSMQ FormatName URIs are scheme-specific (no authority).
            // HttpRequestUtils.GetUrl would mangle this to "msmq.formatname://DIRECT=...".
            var uri = new Uri(@"msmq.formatname:DIRECT=OS:machine\private$\queue");

            var result = WcfCommon.BuildHttpUrl(uri, DefaultManager());

            result.Should().Be(uri.AbsoluteUri);
            result.Should().NotContain("://");
        }

        private static QueryStringManager DefaultManager() =>
            new QueryStringManager(
                reportQueryString: true,
                timeout: 30_000,
                maxSizeBeforeObfuscation: 5000,
                pattern: TracerSettingsConstants.DefaultObfuscationQueryStringRegex);
    }
}

#endif
