// <copyright file="DirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class DirectLogSubmissionSettingsTests : SettingsTestsBase
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("somethingelse.com")]
        public void WhenDirectLogSubmissionUrlIsProvided_UseIt(string ddSite)
        {
            var expected = "http://some_url.com";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.Url, expected },
                { ConfigurationKeys.Site, ddSite },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void WhenUrlIsNotProvided_AndSiteIsProvided_UsesIt(string url)
        {
            var domain = "my-domain.net";
            var expected = "https://http-intake.logs.my-domain.net:443";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.Url, url },
                { ConfigurationKeys.Site, domain },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }

        [Fact]
        public void WhenNeitherSiteOrUrlAreProvided_UsesDefault()
        {
            var expected = "https://http-intake.logs.datadoghq.com:443";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }

        [Fact]
        public void LogSpecificGlobalTagsOverrideTracerGlobalTags()
        {
            var expected = new Dictionary<string, string> { { "test1", "value1" }, { "test2", "value2" }, };

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.GlobalTags, "test1:value1, test2:value2" },
                { ConfigurationKeys.GlobalTags, "test3:value3, test4:value4" },
                { "DD_TRACE_GLOBAL_TAGS", "test5:value5, test6:value6" },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionGlobalTags
                          .Should()
                          .BeEquivalentTo(expected);
        }

        [Fact]
        public void LogSpecificGlobalTagsOverrideTracerGlobalTagsEvenWhenEmpty()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.GlobalTags, string.Empty },
                { ConfigurationKeys.GlobalTags, "test3:value3, test4:value4" },
                { "DD_TRACE_GLOBAL_TAGS", "test5:value5, test6:value6" },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionGlobalTags
                          .Should()
                          .BeEmpty();
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "default", Strings.AllowEmpty)]
        public void DirectLogSubmissionHost(string value, string expected)
        {
            if (expected == "default")
            {
                expected = HostMetadata.Instance.Hostname;
            }

            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.Host, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionHost.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), DirectLogSubmissionSettings.DefaultSource, Strings.AllowEmpty)]
        public void DirectLogSubmissionSource(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.Source, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionSource.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData("trace", DirectSubmissionLogLevel.Verbose)] // Further tested in DirectSubmissionLogLevelExtensionsTests
        public void DirectLogSubmissionMinimumLevel(string value, object expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.MinimumLevel, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionMinimumLevel.Should().Be((DirectSubmissionLogLevel)expected);
        }

        [Theory]
        [InlineData("", new string[0])]
        [InlineData(null, new string[0])]
        [InlineData("test1;TEST1;;test2", new[] { "test1", "test2" })]
        public void DirectLogSubmissionEnabledIntegrations(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionEnabledIntegrations.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultBatchSizeLimit)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultBatchSizeLimit)]
        [InlineData("invalid", DirectLogSubmissionSettings.DefaultBatchSizeLimit)]
        [InlineData("0", DirectLogSubmissionSettings.DefaultBatchSizeLimit)]
        [InlineData("-1", DirectLogSubmissionSettings.DefaultBatchSizeLimit)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionBatchSizeLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionBatchSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultQueueSizeLimit)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultQueueSizeLimit)]
        [InlineData("invalid", DirectLogSubmissionSettings.DefaultQueueSizeLimit)]
        [InlineData("0", DirectLogSubmissionSettings.DefaultQueueSizeLimit)]
        [InlineData("-1", DirectLogSubmissionSettings.DefaultQueueSizeLimit)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionQueueSizeLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.QueueSizeLimit, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionQueueSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultBatchPeriodSeconds)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultBatchPeriodSeconds)]
        [InlineData("invalid", DirectLogSubmissionSettings.DefaultBatchPeriodSeconds)]
        [InlineData("0", DirectLogSubmissionSettings.DefaultBatchPeriodSeconds)]
        [InlineData("-1", DirectLogSubmissionSettings.DefaultBatchPeriodSeconds)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionBatchPeriod(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.DirectLogSubmissionBatchPeriod.Should().Be(TimeSpan.FromSeconds(expected));
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.ApiKey.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void LogsInjectionEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.LogsInjectionEnabled, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.LogsInjectionEnabled.Should().Be(expected);
        }
    }
}
