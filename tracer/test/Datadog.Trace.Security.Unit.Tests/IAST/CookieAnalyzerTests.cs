// <copyright file="CookieAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Bogus;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Http.QueryStringObfuscation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http
{
    [Collection(nameof(EvidenceRedactorTests))]
    public class CookieAnalyzerTests
    {
        private const double Timeout = 10_000;

        [Theory]
        [InlineData("TestCookie", false)]
        [InlineData("TestCookie.0123456789", false)]
        [InlineData("TestCookie.00112233445566778899--", true)]
        [InlineData(".AspNetCore.", false)]
        [InlineData(".AspNetCore.Whatever", false)]
        [InlineData(".AspNetCore.0123456789abcdefghi", false)]
        [InlineData(".AspNetCore.0123456789abcdefghijklmnopqrstuv", true)]
        [InlineData(".Other.0123456789", false)]
        [InlineData("3F53C576-71D7-4CD8-A7D7-D13B9AB48102", true)]
        [InlineData("d54c62958f7893f18924aefb3549bcb0f38d3f0b", true)]

        public void WithDefaultConfigCookieIsFiltered(string pattern, bool mustFilter)
        {
            var cookieAnalyzer = new CookieAnalyzer(true, IastSettings.DefaultCookieFilterRegex, 5000); // Default settings

            cookieAnalyzer.IsFiltered(pattern).Should().Be(mustFilter);
        }

        [Theory]
        [InlineData("TestCookie", false)]
        [InlineData("TestCookie.0123456789", true)]
        [InlineData("TestCookie.00112233445566778899--", true)]
        [InlineData(".AspNetCore.", false)]
        [InlineData(".AspNetCore.Whatever", false)]
        [InlineData(".AspNetCore.0123456789abcdefghi", false)]
        [InlineData(".AspNetCore.0123456789abcdefghijklmnopqrstuv", false)]
        [InlineData(".Other.0123456789", true)]
        [InlineData("3F53C576-71D7-4CD8-A7D7-D13B9AB48102", false)]
        [InlineData("d54c62958f7893f18924aefb3549bcb0f38d3f0b", false)]
        public void WithCustomConfigCookieIsFiltered(string pattern, bool mustFilter)
        {
            var cookieAnalyzer = new CookieAnalyzer(true, @"TestCookie\..*|\.Other\..*", 5000);

            cookieAnalyzer.IsFiltered(pattern).Should().Be(mustFilter);
        }

#if !NETFRAMEWORK

        [Theory]
        [InlineData("TestCookie", false)]
        [InlineData("TestCookie.0123456789", false)]
        [InlineData("TestCookie.00112233445566778899--", false)]
        [InlineData(".AspNetCore.", true)]
        [InlineData(".AspNetCore.Whatever", true)]
        [InlineData(".AspNetCore.0123456789abcdefghi", true)]
        [InlineData(".AspNetCore.0123456789abcdefghijklmnopqrstuv", true)]
        [InlineData(".Other.0123456789", false)]
        [InlineData("3F53C576-71D7-4CD8-A7D7-D13B9AB48102", false)]
        [InlineData("d54c62958f7893f18924aefb3549bcb0f38d3f0b", false)]
        public void CookieIsExcluded(string pattern, bool mustExclude)
        {
            var cookieAnalyzer = new CookieAnalyzer(); // Default settings

            cookieAnalyzer.IsExcluded(pattern).Should().Be(mustExclude);
        }
#endif
    }
}
