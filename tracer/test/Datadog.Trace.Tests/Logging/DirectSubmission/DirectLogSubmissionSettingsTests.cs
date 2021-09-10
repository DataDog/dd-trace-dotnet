// <copyright file="DirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class DirectLogSubmissionSettingsTests
    {
        private static readonly List<string> AllIntegrations =
            DirectLogSubmissionSettings.SupportedIntegrations.Select(x => x.ToString()).ToList();

        private static readonly NameValueCollection Defaults =  new()
        {
            { ConfigurationKeys.LogsInjectionEnabled, "1" },
            { ConfigurationKeys.DirectLogSubmission.Host, "integration_tests" },
            { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", AllIntegrations) },
        };

        [Fact]
        public void ValidSettingsAreValid()
        {
            var settings = SettingsHelper.GetValidSettings();
            settings.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void ValidDefaultsAreValid()
        {
            var apiKey = "some_key";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(Defaults));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public void InvalidApiKeyIsInvalid(string apiKey)
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(Defaults));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            logSettings.IsEnabled.Should().BeFalse();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Fact]
        public void WarnsButDoesntDisableWhenLogsInjectionDisabled()
        {
            var apiKey = "some_key";
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[ConfigurationKeys.LogsInjectionEnabled] = "0";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            logSettings.IsEnabled.Should().BeTrue();
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
            var apiKey = "some_key";
            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[ConfigurationKeys.DirectLogSubmission.MinimumLevel] = value;
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            logSettings.IsEnabled.Should().BeTrue();
            logSettings.ValidationErrors.Should().BeEmpty();
            logSettings.MinimumLevel.Should().Be(expected);
        }

        [Theory]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Host, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Host, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Source, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Source, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Url, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Url, "   ")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Url, "localhost")]
        public void InvalidSettingDisablesDirectLogSubmission(string setting, string value)
        {
            var apiKey = "some_key";

            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[setting] = value;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            logSettings.IsEnabled.Should().BeFalse();
            logSettings.ValidationErrors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(ConfigurationKeys.LogsInjectionEnabled, "0")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, "Garbage")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Transport, "")]
        [InlineData(ConfigurationKeys.DirectLogSubmission.Transport, "FILE")]
        public void InvalidSettingWarnsButDoesNotDisableDirectLogSubmission(string setting, string value)
        {
            var apiKey = "some_key";

            var updatedSettings = new NameValueCollection(Defaults);
            updatedSettings[setting] = value;

            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(updatedSettings));
            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

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
                { ConfigurationKeys.DirectLogSubmission.Host, hostName },
                { ConfigurationKeys.DirectLogSubmission.Url, intake },
                { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, string.Join(";", enabledIntegrations) },
                { ConfigurationKeys.DirectLogSubmission.GlobalTags, "sometag:value" },
            };

            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);

            var logSettings = DirectLogSubmissionSettings.Create(tracerSettings, apiKey, GetIntegrationSettings(tracerSettings));

            using var scope = new AssertionScope();
            logSettings.ApiKey.Should().Be(apiKey);
            logSettings.Host.Should().Be(hostName);
            logSettings.IntakeUrl?.ToString().Should().Be("http://localhost:1234/");
            logSettings.Transport.Should().Be(LogsTransportStrategy.Http);
            logSettings.GlobalTags.Should().Be("sometag:value");
            logSettings.IsEnabled.Should().BeTrue();
            logSettings.MinimumLevel.Should().Be(DirectSubmissionLogLevel.Information);
            logSettings.Source.Should().Be("csharp");
            logSettings.ValidationErrors.Should().BeEmpty();
            logSettings.EnabledIntegrationNames.Should().Equal(enabledIntegrations);
        }

        private static ImmutableIntegrationSettingsCollection GetIntegrationSettings(TracerSettings tracerSettings)
        {
            return new ImmutableIntegrationSettingsCollection(tracerSettings.Integrations, tracerSettings.DisabledIntegrationNames);
        }
    }
}
