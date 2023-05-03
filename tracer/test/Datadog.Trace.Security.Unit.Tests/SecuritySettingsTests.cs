// <copyright file="SecuritySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecuritySettingsTests : SettingsTestsBase
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("gibberish")]
        [InlineData("5mins")] // unknown suffix ending in 's'
        [InlineData("500d")] // unknown suffix
        public void WafTimeoutInvalid(string value)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.WafTimeout, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(100_000ul, settings.WafTimeoutMicroSeconds);
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
        public void WafTimeoutValid(string value, ulong expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.WafTimeout, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.WafTimeoutMicroSeconds);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.BlockedHtmlTemplate, Strings.AllowEmpty)]
        public void BlockedHtmlTemplate(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.HtmlBlockedTemplate, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.BlockedHtmlTemplate);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.BlockedJsonTemplate, Strings.AllowEmpty)]
        public void BlockedJsonTemplate(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.JsonBlockedTemplate, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.BlockedJsonTemplate);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Enabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.Enabled);
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("false", false)]
        [InlineData(null, true)]
        public void CanBeToggled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.CanBeToggled);
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData(null, null)]
        public void Rules(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Rules, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.Rules);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CustomIpHeader(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.CustomIpHeader, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.CustomIpHeader);
        }

        [Theory]
        [InlineData("test", new[] { "test" })]
        [InlineData("test1,test2", new[] { "test1", "test2" })]
        [InlineData(null, new string[0])]
        [InlineData("", new string[0])]
        public void ExtraHeaders(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ExtraHeaders, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.ExtraHeaders);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void KeepTraces(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.KeepTraces, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.KeepTraces);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 100)]
        public void TraceRateLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.TraceRateLimit, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.TraceRateLimit);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.ObfuscationParameterKeyRegexDefault, Strings.DisallowEmpty)]
        public void ObfuscationParameterKeyRegex(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ObfuscationParameterKeyRegex, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.ObfuscationParameterKeyRegex);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.ObfuscationParameterValueRegexDefault, Strings.DisallowEmpty)]
        public void ObfuscationParameterValueRegex(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ObfuscationParameterValueRegex, value));
            var settings = new SecuritySettings(source);

            Assert.Equal(expected, settings.ObfuscationParameterValueRegex);
        }
    }
}
