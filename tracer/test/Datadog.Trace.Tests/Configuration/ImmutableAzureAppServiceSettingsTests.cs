// <copyright file="ImmutableAzureAppServiceSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
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
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.IsUnsafeToTrace.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("test", "test")]
        [InlineData("+", null)]
        [InlineData("test1+test2", "test1")]
        public void SubscriptionId(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.SubscriptionId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ResourceGroup(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.ResourceGroupKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.ResourceGroup.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void SiteName(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.SiteNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.SiteName.Should().Be(expected);
        }

        [Theory]
        [InlineData("Subscription", "Sitename", "Resourcegroup", "/subscriptions/subscription/resourcegroups/resourcegroup/providers/microsoft.web/sites/sitename")]
        [InlineData(null, "Sitename", "Resourcegroup", null)]
        [InlineData("Subscription", null, "Resourcegroup", null)]
        [InlineData("Subscription", "Sitename", null, null)]
        [InlineData("", "", "", null)]
        public void ResourceId(string subscriptionId, string siteName, string resourceGroup, string expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.AzureAppService.SiteNameKey, siteName),
                (ConfigurationKeys.AzureAppService.ResourceGroupKey, resourceGroup),
                (ConfigurationKeys.AzureAppService.WebsiteOwnerNameKey, subscriptionId));

            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.ResourceId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void InstanceId(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.InstanceIdKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.InstanceId.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void InstanceName(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.InstanceNameKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.InstanceName.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void OperatingSystem(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.OperatingSystemKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.OperatingSystem.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "unknown", Strings.AllowEmpty)]
        public void SiteExtensionVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.SiteExtensionVersion.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void FunctionsWorkerRuntime(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.FunctionsWorkerRuntime.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void FunctionsExtensionVersion(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.FunctionsExtensionVersion.Should().Be(expected);
        }

        [Theory]
        [InlineData("value", null, Trace.PlatformHelpers.AzureContext.AzureFunctions)]
        [InlineData(null, "value", Trace.PlatformHelpers.AzureContext.AzureFunctions)]
        [InlineData(null, null, Trace.PlatformHelpers.AzureContext.AzureAppService)]
        public void AzureContext(string functionsWorkerRuntime, string functionsExtensionVersion, object expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.AzureAppService.FunctionsWorkerRuntimeKey, functionsWorkerRuntime),
                (ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, functionsExtensionVersion));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.AzureContext.Should().Be((AzureContext)expected);
        }

        [Fact]
        public void Runtime()
        {
            var source = CreateConfigurationSource();
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.Runtime.Should().Be(FrameworkDescription.Instance.Name);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void DebugModeEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DebugEnabled, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.DebugModeEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void CustomTracingEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AasEnableCustomTracing, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.CustomTracingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void NeedsDogStatsD(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.AzureAppService.AasEnableCustomMetrics, value));
            var settings = new ImmutableAzureAppServiceSettings(source);

            settings.NeedsDogStatsD.Should().Be(expected);
        }
    }
}
