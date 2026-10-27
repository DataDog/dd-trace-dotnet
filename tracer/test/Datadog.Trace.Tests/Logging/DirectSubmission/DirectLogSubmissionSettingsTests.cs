// <copyright file="DirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Configuration;
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
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

            settings.Host.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), DirectLogSubmissionSettings.DefaultSource, Strings.AllowEmpty)]
        public void DirectLogSubmissionSource(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.Source, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

            settings.Source.Should().Be(expected);
        }

        [Theory]
        [InlineData("", DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData(null, DirectLogSubmissionSettings.DefaultMinimumLevel)]
        [InlineData("trace", DirectSubmissionLogLevel.Verbose)] // Further tested in DirectSubmissionLogLevelExtensionsTests
        public void DirectLogSubmissionMinimumLevel(string value, object expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DirectLogSubmission.MinimumLevel, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

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
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

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
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

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
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

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
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

            settings.BatchPeriod.Should().Be(TimeSpan.FromSeconds(expected));
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "", Strings.AllowEmpty)]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new DirectLogSubmissionSettings(source, NullConfigurationTelemetry.Instance, otlpLogsEnabled: false, new OverrideErrorLog());

            settings.ApiKey.Should().Be(expected);
        }

        [Fact]
        public void ValidSettingsAreValid()
        {
            var settings = LogSettingsHelper.GetValidSettings().LogSubmissionSettings;
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
            tracerSettings.Manager.InitialMutableSettings.LogsInjectionEnabled.Should().Be(logsInjectionEnabled);
        }

        [Theory]
        [InlineData("17", null, 17)]
        [InlineData(null, null, DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        [InlineData("", null, DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        [InlineData("0", null, DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        [InlineData("-1", null, DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        [InlineData("abc", null, DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        [InlineData("500", "2", 2000)]
        [InlineData("500", "0", DirectLogSubmissionSettings.OtlpBatchPeriodMs)]
        public void OtlpScheduleDelay(string otel, string dd, int expectedMs)
        {
            var settings = OtlpEnabled(
                (ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, otel),
                (ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, dd));

            settings.BatchPeriod.Should().Be(TimeSpan.FromMilliseconds(expectedMs));
        }

        [Fact]
        public void OtlpScheduleDelayZero_LogsInvalidConfiguration()
        {
            var errorLog = new OverrideErrorLog();
            OtlpEnabled(errorLog, (ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, "0"));

            errorLog.ShouldHaveExpectedOtelMetric(
                Count.OpenTelemetryConfigInvalid,
                ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs.ToLowerInvariant(),
                ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds.ToLowerInvariant());
        }

        [Theory]
        [InlineData("1024", null, 1024)]
        [InlineData(null, null, DirectLogSubmissionSettings.OtlpQueueSizeLimit)]
        [InlineData("", null, DirectLogSubmissionSettings.OtlpQueueSizeLimit)]
        [InlineData("0", null, DirectLogSubmissionSettings.OtlpQueueSizeLimit)]
        [InlineData("-5", null, DirectLogSubmissionSettings.OtlpQueueSizeLimit)]
        [InlineData("1024", "300", 300)]
        public void OtlpMaxQueueSize(string otel, string dd, int expected)
        {
            var settings = OtlpEnabled(
                (ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize, otel),
                (ConfigurationKeys.DirectLogSubmission.QueueSizeLimit, dd));

            settings.QueueSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData("64", null, null, 64)]
        [InlineData(null, null, null, DirectLogSubmissionSettings.OtlpBatchSizeLimit)]
        [InlineData("0", null, null, DirectLogSubmissionSettings.OtlpBatchSizeLimit)]
        [InlineData("2049", null, null, DirectLogSubmissionSettings.OtlpBatchSizeLimit)]
        [InlineData("200", "100", null, 100)]
        [InlineData("64", null, "128", 128)]
        public void OtlpMaxExportBatchSize(string otelBatch, string otelQueue, string ddBatch, int expected)
        {
            var settings = OtlpEnabled(
                (ConfigurationKeys.OpenTelemetry.BlrpMaxExportBatchSize, otelBatch),
                (ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize, otelQueue),
                (ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, ddBatch));

            settings.BatchSizeLimit.Should().Be(expected);
        }

        [Theory]
        [InlineData(ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds)]
        [InlineData(ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize, ConfigurationKeys.DirectLogSubmission.QueueSizeLimit)]
        [InlineData(ConfigurationKeys.OpenTelemetry.BlrpMaxExportBatchSize, ConfigurationKeys.DirectLogSubmission.BatchSizeLimit)]
        public void OtlpDuplicateKeysLogHiddenMetric(string otelKey, string datadogKey)
        {
            var errorLog = new OverrideErrorLog();
            OtlpEnabled(errorLog, (otelKey, "64"), (datadogKey, "8"));

            errorLog.ShouldHaveExpectedOtelMetric(
                Count.OpenTelemetryConfigHiddenByDatadogConfig,
                otelKey.ToLowerInvariant(),
                datadogKey.ToLowerInvariant());
        }

        [Fact]
        public void OtlpScheduleDelayInvalidDatadogAndValidOtel_LogsHiddenMetric()
        {
            var errorLog = new OverrideErrorLog();
            OtlpEnabled(
                errorLog,
                (ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, "500"),
                (ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, "0"));

            errorLog.ShouldHaveExpectedOtelMetric(
                Count.OpenTelemetryConfigHiddenByDatadogConfig,
                ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs.ToLowerInvariant(),
                ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds.ToLowerInvariant());
        }

        [Fact]
        public void OtlpScheduleDelayDefault_IsRecordedUnderDatadogKey()
        {
            var telemetry = new ConfigurationTelemetry();

            _ = new DirectLogSubmissionSettings(CreateConfigurationSource(), telemetry, otlpLogsEnabled: true, new OverrideErrorLog());

            telemetry.GetQueueForTesting()
                     .Where(x => x.Key is ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds or ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs)
                     .Select(x => (x.Key, x.IntValue, x.Origin))
                     .Should()
                     .Equal((ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, (int?)1, ConfigurationOrigins.Default));
        }

        [Theory]
        [InlineData("2049", null, null, DirectLogSubmissionSettings.OtlpBatchSizeLimit)]
        [InlineData(null, null, "3000", DirectLogSubmissionSettings.OtlpBatchSizeLimit)]
        [InlineData(null, "100", null, 100)]
        public void OtlpBatchSizeGreaterThanQueue_RecordsCalculatedTelemetryUnderDatadogKey(string otelBatch, string otelQueue, string ddBatch, int expectedValue)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.OpenTelemetry.BlrpMaxExportBatchSize, otelBatch),
                (ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize, otelQueue),
                (ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, ddBatch));
            var telemetry = new ConfigurationTelemetry();

            _ = new DirectLogSubmissionSettings(source, telemetry, otlpLogsEnabled: true, new OverrideErrorLog());

            telemetry.GetQueueForTesting()
                     .Where(x => x.Origin == ConfigurationOrigins.Calculated)
                     .Select(x => (x.Key, x.IntValue))
                     .Should()
                     .Equal((ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, (int?)expectedValue));
        }

        [Fact]
        public void OtelKeysIgnoredWhenOtlpDisabled()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, "17"),
                (ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize, "1024"),
                (ConfigurationKeys.OpenTelemetry.BlrpMaxExportBatchSize, "64"));
            var telemetry = new ConfigurationTelemetry();

            var settings = new DirectLogSubmissionSettings(source, telemetry, otlpLogsEnabled: false, new OverrideErrorLog());

            using var scope = new AssertionScope();
            settings.BatchSizeLimit.Should().Be(DirectLogSubmissionSettings.DatadogDefaultBatchSizeLimit);
            settings.QueueSizeLimit.Should().Be(DirectLogSubmissionSettings.DatadogDefaultQueueSizeLimit);
            settings.BatchPeriod.Should().Be(TimeSpan.FromSeconds(DirectLogSubmissionSettings.DatadogDefaultBatchPeriodSeconds));
            telemetry.GetQueueForTesting().Select(x => x.Key).Should().NotContain(
                [
                    ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs,
                    ConfigurationKeys.OpenTelemetry.BlrpMaxQueueSize,
                    ConfigurationKeys.OpenTelemetry.BlrpMaxExportBatchSize,
                ]);
        }

        [Theory]
        [InlineData(ConfigurationKeys.OpenTelemetry.BlrpScheduleDelayMs, "17", 17, 1000)]
        [InlineData(ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, "5", 5000, 5000)]
        public void CreateBatchingSinkOptions_CircuitBreakPeriod(string key, string value, int expectedPeriodMs, int expectedCircuitBreakPeriodMs)
        {
            var options = OtlpEnabled((key, value)).CreateBatchingSinkOptions();

            options.Period.Should().Be(TimeSpan.FromMilliseconds(expectedPeriodMs));
            options.CircuitBreakPeriod.Should().Be(TimeSpan.FromMilliseconds(expectedCircuitBreakPeriodMs));
        }

        private static DirectLogSubmissionSettings OtlpEnabled(params (string Key, string Value)[] values)
            => OtlpEnabled(new OverrideErrorLog(), values);

        private static DirectLogSubmissionSettings OtlpEnabled(OverrideErrorLog errorLog, params (string Key, string Value)[] values)
            => new(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance, otlpLogsEnabled: true, errorLog);
    }
}
