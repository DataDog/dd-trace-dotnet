// <copyright file="DirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class DirectLogSubmissionSettingsTests : SettingsTestsBase
    {
        private static readonly List<string> AllIntegrations =
            DirectLogSubmissionSettings.SupportedIntegrations.Select(x => x.ToString()).ToList();

        private static readonly NameValueCollection Defaults =  new()
        {
            { ConfigurationKeys.LogsInjectionEnabled, "1" },
            { ConfigurationKeys.ApiKey, "some_key" },
            { ConfigurationKeys.DirectLogSubmission.Host, "integration_tests" },
            { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", AllIntegrations) },
        };

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

            tracerSettings.LogSubmissionSettings.IntakeUrl.Should().Be(expected);
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

            tracerSettings.LogSubmissionSettings.IntakeUrl.Should().Be(expected);
        }

        [Fact]
        public void WhenNeitherSiteOrUrlAreProvided_UsesDefault()
        {
            var expected = "https://http-intake.logs.datadoghq.com:443";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));

            tracerSettings.LogSubmissionSettings.IntakeUrl.Should().Be(expected);
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

            tracerSettings.LogSubmissionSettings.GlobalTags
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

            tracerSettings.LogSubmissionSettings.GlobalTags
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

            settings.Host.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), DirectLogSubmissionSettings.DefaultSource, Strings.AllowEmpty)]
        public void DirectLogSubmissionSource(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.Source, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.Source.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData("trace", DirectSubmissionLogLevel.Verbose)] // Further tested in DirectSubmissionLogLevelExtensionsTests
        public void DirectLogSubmissionMinimumLevel(string value, object expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.MinimumLevel, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.MinimumLevel.Should().Be((DirectSubmissionLogLevel)expected);
        }

        [Theory]
        [InlineData("", new string[0])]
        [InlineData(null, new string[0])]
        [InlineData("test1;TEST1;;test2", new string[0])]
        [InlineData("serilog;SERILOG;;NLog", new[] { "Serilog", "NLog" })]
        public void DirectLogSubmissionEnabledIntegrations(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.EnabledIntegrationNames.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit)]
        [InlineData(null, DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit)]
        [InlineData("invalid", DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit)]
        [InlineData("0", DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit)]
        [InlineData("-1", DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionBatchSizeLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.BatchSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit)]
        [InlineData(null, DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit)]
        [InlineData("invalid", DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit)]
        [InlineData("0", DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit)]
        [InlineData("-1", DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionQueueSizeLimit(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.QueueSizeLimit, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.QueueSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds)]
        [InlineData(null, DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds)]
        [InlineData("invalid", DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds)]
        [InlineData("0", DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds)]
        [InlineData("-1", DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds)]
        [InlineData("256", 256)]
        public void DirectLogSubmissionBatchPeriod(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.BatchPeriod.Should().Be(TimeSpan.FromSeconds(expected));
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "", Strings.AllowEmpty)]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance);

            settings.ApiKey.Should().Be(expected);
        }

        [Fact]
        public void ValidSettingsAreValid()
        {
            var settings = LogSettingsHelper.GetValidSettings();
            settings.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void ValidDefaultsAreValid()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(Defaults));
            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public void InvalidApiKeyIsInvalid(string apiKey)
        {
            var nameValueCollection = new NameValueCollection(Defaults);
            nameValueCollection[ConfigurationKeys.ApiKey] = apiKey;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(nameValueCollection));

            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeFalse();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("VeryVerbose", DirectSubmissionLogLevel.Information)]
        [InlineData("", DirectSubmissionLogLevel.Information)]
        [InlineData(null, DirectSubmissionLogLevel.Information)]
        [InlineData("Verbose", DirectSubmissionLogLevel.Verbose)]
        [InlineData("trace", DirectSubmissionLogLevel.Verbose)]
        [InlineData("Debug", DirectSubmissionLogLevel.Debug)]
        [InlineData("Info", DirectSubmissionLogLevel.Information)]
        [InlineData("Warning", DirectSubmissionLogLevel.Warning)]
        [InlineData("ERROR", DirectSubmissionLogLevel.Error)]
        [InlineData("Fatal", DirectSubmissionLogLevel.Fatal)]
        [InlineData("critical", DirectSubmissionLogLevel.Fatal)]
        public void ParsesLogLevelsCorrectly(string value, DirectSubmissionLogLevel expected)
        {
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[ConfigurationKeys.DirectLogSubmission.MinimumLevel] = value;
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().BeEmpty();
            logSettings.MinimumLevel.Should().Be(expected);
        }

        [Theory]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Host, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Host, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Source, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Source, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Url, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Url, "localhost")]
        public void InvalidSettingDisablesDirectLogSubmission(string setting, string value)
        {
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[setting] = value;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeFalse();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, "Serilog;Garbage")]
        public void InvalidSettingWarnsButDoesNotDisableDirectLogSubmission(string setting, string value)
        {
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[setting] = value;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Fact]
        public void CanLoadSettingsFromTracerSettings()
        {
            var apiKey = "some_key";
            var hostName = "integration_tests";
            var intake = "http://localhost:1234";
            var enabledIntegrations = DirectLogSubmissionSettings.SupportedIntegrations
                                                                 .Select(x => x.ToString())
                                                                 .ToList();

            var collection = new NameValueCollection
            {
                { ConfigurationKeys.LogsInjectionEnabled, "1" },
                { ConfigurationKeys.ApiKey, apiKey },
                { ConfigurationKeys.DirectLogSubmission.Host, hostName },
                { ConfigurationKeys.DirectLogSubmission.Url, intake },
                { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", enabledIntegrations) },
                { ConfigurationKeys.DirectLogSubmission.GlobalTags, "sometag:value, someothertag:someothervalue" },
            };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);

            var logSettings = tracerSettings.LogSubmissionSettings;

            using var scope = new AssertionScope();
            logSettings.ApiKey.Should().Be(apiKey);
            logSettings.Host.Should().Be(hostName);
            logSettings.IntakeUrl?.ToString().Should().Be("http://localhost:1234/");
            logSettings.GlobalTags.Should().BeEquivalentTo(new KeyValuePair<string, string>[] { new("someothertag", "someothervalue"), new("sometag", "value") });
            logSettings.IsEnabled.Should().BeTrue();
            logSettings.MinimumLevel.Should().Be(DirectSubmissionLogLevel.Information);
            logSettings.Source.Should().Be("csharp");
            logSettings.ValidationErrors.Should().BeEmpty();
            logSettings.EnabledIntegrationNames.Should().Equal(enabledIntegrations);
        }

        [Theory]
        [InlineData("nlog")]
        [InlineData("NLOG")]
        [InlineData("nLog")]
        [InlineData("Nlog")]
        [InlineData("NLog")]
        [InlineData("nLog;nlog;Nlog")]
        public void EnabledIntegrationsAreCaseInsensitive(string integration)
        {
            var config = new NameValueCollection(Defaults);
            config[ConfigurationKeys.DirectLogSubmission.EnabledIntegrations] = integration;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(config));
            var logSettings = tracerSettings.LogSubmissionSettings;

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().BeEmpty();
            var expected = new List<string> { nameof(IntegrationId.NLog) };
            logSettings.EnabledIntegrationNames.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void WhenLogsInjectionIsExplicitlySetViaEnvironmentThenValueIsUsed(bool logsInjectionEnabled, bool directLogSubmissionEnabled)
        {
            var config = new NameValueCollection(Defaults);
            config[ConfigurationKeys.LogsInjectionEnabled] = logsInjectionEnabled.ToString();

            if (!directLogSubmissionEnabled)
            {
                config.Remove(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations);
            }

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(config));

            tracerSettings.LogSubmissionSettings.IsEnabled.Should().Be(directLogSubmissionEnabled);
            tracerSettings.MutableSettings.LogsInjectionEnabled.Should().Be(logsInjectionEnabled);
        }
    }
}
