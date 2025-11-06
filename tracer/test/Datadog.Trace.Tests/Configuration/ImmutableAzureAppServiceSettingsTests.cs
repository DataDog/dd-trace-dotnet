// <copyright file="ImmutableAzureAppServiceSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableAzureAppServiceSettingsTests : SettingsTestsBase
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("any value", false)]
        public void IsUnsafeToTrace(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.IsUnsafeToTrace.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("  ", null)]
        [InlineData("test", "test")]
        [InlineData("+test", null)]
        [InlineData("+", null)]
        [InlineData("test1+test2", "test1")]
        public void SubscriptionId(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.WebsiteOwnerNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.SubscriptionId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ResourceGroup(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.ResourceGroupKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.ResourceGroup.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void SiteName(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.SiteNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.SiteName.Should().Be(expected);
        }

        [Theory]
        [InlineData("Subscription", "Sitename", "Resourcegroup", "/subscriptions/subscription/resourcegroups/resourcegroup/providers/microsoft.web/sites/sitename")]
        [InlineData("Subscription", "", "", "/subscriptions/subscription/resourcegroups//providers/microsoft.web/sites/")]
        [InlineData(null, "Sitename", "Resourcegroup", null)]
        [InlineData("Subscription", null, "Resourcegroup", null)]
        [InlineData("Subscription", "Sitename", null, null)]
        [InlineData("", "", "", null)]
        public void ResourceId(string subscriptionId, string siteName, string resourceGroup, string expected)
        {
            var source = CreateConfigurationSource(
                (PlatformKeys.AzureAppService.SiteNameKey, siteName),
                (PlatformKeys.AzureAppService.ResourceGroupKey, resourceGroup),
                (PlatformKeys.AzureAppService.WebsiteOwnerNameKey, subscriptionId));

            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.ResourceId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void InstanceId(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.InstanceIdKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.InstanceId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void InstanceName(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.InstanceNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.InstanceName.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void OperatingSystem(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureAppService.OperatingSystemKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.OperatingSystem.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void SiteExtensionVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.SiteExtensionVersion.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void FunctionsWorkerRuntime(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.FunctionsWorkerRuntime.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void FunctionsExtensionVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((PlatformKeys.AzureFunctions.FunctionsExtensionVersion, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.FunctionsExtensionVersion.Should().Be(expected);
        }

        [Theory]
        [InlineData("value", null, false)]
        [InlineData(null, "value", false)]
        [InlineData(null, null, false)]
        [InlineData("value", "value", true)]

        public void IsFunctionsApp(string functionsWorkerRuntime, string functionsExtensionVersion, bool expected)
        {
            var source = CreateConfigurationSource(
                (PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, functionsWorkerRuntime),
                (PlatformKeys.AzureFunctions.FunctionsExtensionVersion, functionsExtensionVersion));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.IsFunctionsApp.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void DebugModeEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DebugEnabled, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.DebugModeEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void CustomTracingEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AasEnableCustomTracing, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.CustomTracingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void NeedsDogStatsD(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AasEnableCustomMetrics, value));
            var settings = new ImmutableAzureAppServiceSettings(source, NullConfigurationTelemetry.Instance);

            settings.NeedsDogStatsD.Should().Be(expected);
        }
    }
}
