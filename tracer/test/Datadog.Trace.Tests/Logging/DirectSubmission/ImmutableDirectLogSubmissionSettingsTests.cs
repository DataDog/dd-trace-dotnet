// <copyright file="ImmutableDirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class ImmutableDirectLogSubmissionSettingsTests
    {
        private static readonly List<string> AllIntegrations =
            ImmutableDirectLogSubmissionSettings.SupportedIntegrations.Select(x => x.ToString()).ToList();

        private static readonly NameValueCollection Defaults =  new()
        {
            { ConfigurationKeys.LogsInjectionEnabled, "1" },
            { ConfigurationKeys.ApiKey, "some_key" },
            { ConfigurationKeys.DirectLogSubmission.Host, "integration_tests" },
            { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", AllIntegrations) },
        };

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
            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

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

            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

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
            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

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
            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

            logSettings.IsEnabled.Should().BeFalse();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, "Garbage")]
        public void InvalidSettingWarnsButDoesNotDisableDirectLogSubmission(string setting, string value)
        {
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[setting] = value;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Fact]
        public void CanLoadSettingsFromTracerSettings()
        {
            var apiKey = "some_key";
            var hostName = "integration_tests";
            var intake = "http://localhost:1234";
            var enabledIntegrations = ImmutableDirectLogSubmissionSettings.SupportedIntegrations
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

            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

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
            var logSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);

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
            var immutableSettings = new ImmutableTracerSettings(tracerSettings);

            immutableSettings.LogSubmissionSettings.IsEnabled.Should().Be(directLogSubmissionEnabled);
            immutableSettings.LogsInjectionEnabled.Should().Be(logsInjectionEnabled);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void WhenLogsInjectionIsExplicitlySetViaCodeThenValueIsUsed(bool logsInjectionEnabled, bool directLogSubmissionEnabled)
        {
            var config = new NameValueCollection(Defaults);
            config.Remove(ConfigurationKeys.LogsInjectionEnabled);

            if (!directLogSubmissionEnabled)
            {
                config.Remove(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations);
            }

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(config));
            tracerSettings.LogsInjectionEnabled = logsInjectionEnabled;

            var immutableSettings = new ImmutableTracerSettings(tracerSettings);

            immutableSettings.LogSubmissionSettings.IsEnabled.Should().Be(directLogSubmissionEnabled);
            immutableSettings.LogsInjectionEnabled.Should().Be(logsInjectionEnabled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhenLogsInjectionIsNotSetThenValueIsSameAsLogSubmissionEnabled(bool directLogSubmissionEnabled)
        {
            var config = new NameValueCollection(Defaults);
            config.Remove(ConfigurationKeys.LogsInjectionEnabled);

            if (!directLogSubmissionEnabled)
            {
                config.Remove(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations);
            }

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(config));
            var immutableSettings = new ImmutableTracerSettings(tracerSettings);

            immutableSettings.LogSubmissionSettings.IsEnabled.Should().Be(directLogSubmissionEnabled);
            immutableSettings.LogsInjectionEnabled.Should().Be(directLogSubmissionEnabled);
        }
    }
}
