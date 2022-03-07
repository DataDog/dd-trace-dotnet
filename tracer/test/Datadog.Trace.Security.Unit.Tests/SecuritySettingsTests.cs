// <copyright file="SecuritySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecuritySettingsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("gibberish")]
        [InlineData("5mins")] // unknown suffix ending in 's'
        [InlineData("500d")] // unknown suffix
        public void InvalidValuesUseDefault(string value)
        {
            var target = CreateTestTarget(value);

            Assert.Equal(100_000ul, target.WafTimeoutMicroSeconds);
        }

        [Theory]
        [InlineData("500", 500ul)]
        [InlineData("5s", 5_000_000ul)]
        [InlineData("50ms", 50_000ul)]
        [InlineData("500us", 500ul)]
        [InlineData("500us  ", 500ul)]
        [InlineData("  500us", 500ul)]
        [InlineData("  500us  ", 500ul)]
        [InlineData("  500 us  ", 500ul)]
        public void ParsesValueCorrectly(string value, ulong expected)
        {
            var target = CreateTestTarget(value);

            Assert.Equal(expected, target.WafTimeoutMicroSeconds);
        }

        private static SecuritySettings CreateTestTarget(string stringToBeParsed)
        {
            var config = new NameValueCollection() { { ConfigurationKeys.AppSecWafTimeout, stringToBeParsed } };

            var target = new SecuritySettings(new NameValueConfigurationSource(config));
            return target;
        }
    }
}
