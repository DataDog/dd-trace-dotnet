// <copyright file="SecuritySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

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
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            Assert.Equal(expected, settings.WafTimeoutMicroSeconds);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void BlockedHtmlTemplate(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.HtmlBlockedTemplate, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.BlockedHtmlTemplatePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void BlockedJsonTemplate(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.JsonBlockedTemplate, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.BlockedJsonTemplatePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Enabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.Enabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("false", false)]
        [InlineData(null, true)]
        public void CanBeToggled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.CanBeToggled.Should().Be(expected);
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData(null, null)]
        public void Rules(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Rules, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.Rules.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CustomIpHeader(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.CustomIpHeader, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.CustomIpHeader.Should().Be(expected);
        }

        [Theory]
        [InlineData("test", new[] { "test" })]
        [InlineData("test1,test2", new[] { "test1", "test2" })]
        [InlineData(null, new string[0])]
        [InlineData("", new string[0])]
        public void ExtraHeaders(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ExtraHeaders, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.ExtraHeaders.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void KeepTraces(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.KeepTraces, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.KeepTraces.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Int32TestCases), 100)]
        public void TraceRateLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.TraceRateLimit, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.TraceRateLimit.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.ObfuscationParameterKeyRegexDefault, Strings.DisallowEmpty)]
        public void ObfuscationParameterKeyRegex(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ObfuscationParameterKeyRegex, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.ObfuscationParameterKeyRegex.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), SecurityConstants.ObfuscationParameterValueRegexDefault, Strings.DisallowEmpty)]
        public void ObfuscationParameterValueRegex(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.ObfuscationParameterValueRegex, value));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.ObfuscationParameterValueRegex.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void RaspEnabledAsmEnabled(string raspEnabledValue, bool expected)
        {
            var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.Enabled, "true"), (ConfigurationKeys.AppSec.RaspEnabled, raspEnabledValue)]);
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.RaspEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("false", false)]
        public void RaspEnabledAsmDisabled(string raspEnabledValue, bool expected)
        {
            var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.Enabled, "false"), (ConfigurationKeys.AppSec.RaspEnabled, raspEnabledValue)]);
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.RaspEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void StackTraceEnabled(string stackTraceEnabledValue, bool expected)
        {
            var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.StackTraceEnabled, stackTraceEnabledValue)]);
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.StackTraceEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("0", 32)]
        [InlineData("100", 100)]
        [InlineData("-1", 32)]
        [InlineData("AAA", 32)]
        public void MaxStackTraceDepth(string maxStackTraceDepthValue, int expected)
        {
            var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.MaxStackTraceDepth, maxStackTraceDepthValue)]);
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.MaxStackTraceDepth.Should().Be(expected);
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("0", 2)]
        [InlineData("100", 100)]
        [InlineData("-1", 2)]
        [InlineData("AAA", 2)]
        public void MaxStackTraces(string maxStackTracesValue, int expected)
        {
            var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.MaxStackTraces, maxStackTracesValue)]);
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

            settings.MaxStackTraces.Should().Be(expected);
        }
    }
}
