// <copyright file="ConfigurationTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class ConfigurationTelemetryCollectorTests
    {
        private const string ServiceName = "serializer-test-app";
        private static readonly AzureAppServices EmptyAasSettings = new(new Dictionary<string, string>());

        [Fact]
        public void HasChangesAfterEachTracerSettingsAdded()
        {
            var settings = new ImmutableTracerSettings(new TracerSettings());

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(settings, ServiceName, EmptyAasSettings);

            collector.HasChanges().Should().BeTrue();
            var data = collector.GetConfigurationData();
            data.TracerInstanceCount = 1;
            collector.HasChanges().Should().BeFalse();

            collector.RecordTracerSettings(settings, ServiceName, EmptyAasSettings);
            collector.HasChanges().Should().BeTrue();

            data = collector.GetConfigurationData();
            data.TracerInstanceCount = 2;
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void ApplicationDataShouldIncludeExpectedValues()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, EmptyAasSettings);

            var data = collector.GetApplicationData();

            data.ServiceName.Should().Be(ServiceName);
            data.Env.Should().Be(env);
            data.TracerVersion.Should().Be(TracerConstants.AssemblyVersion);
            data.LanguageName.Should().Be("dotnet");
            data.ServiceVersion.Should().Be(serviceVersion);
            data.LanguageVersion.Should().Be(FrameworkDescription.Instance.ProductVersion);
            data.RuntimeName.Should().NotBeNullOrEmpty().And.Be(FrameworkDescription.Instance.Name);
            data.RuntimeVersion.Should().BeNull();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ConfigurationDataShouldIncludeExpectedSecurityValues(bool enabled, bool blockingEnabled)
        {
            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName, EmptyAasSettings);
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.AppSecEnabled, enabled.ToString() },
                { ConfigurationKeys.AppSecBlockingEnabled, blockingEnabled.ToString() },
            });
            collector.RecordSecuritySettings(new SecuritySettings(source));

            var data = collector.GetConfigurationData();

            data.SecurityEnabled.Should().Be(enabled);
            data.SecurityBlockingEnabled.Should().Be(blockingEnabled);
        }

        [Fact]
        public void ConfigurationDataShouldIncludeAzureValuesWhenInAzureAndSafeToTrace()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };
            var aas = new AzureAppServices(new Dictionary<string, string>
            {
                { ConfigurationKeys.ApiKey, "SomeValue" },
                { AzureAppServices.AzureAppServicesContextKey, "1" },
                { AzureAppServices.SiteExtensionVersionKey, "1.5.0" },
                { AzureAppServices.FunctionsExtensionVersionKey, "~3" },
            });

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, aas);

            var data = collector.GetConfigurationData();

            using var scope = new AssertionScope();
            data.AasConfigurationError.Should().BeFalse();
            data.CloudHosting.Should().Be("Azure");
            data.AasAppType.Should().Be("function");
            data.AasFunctionsRuntimeVersion.Should().Be("~3");
            data.AasSiteExtensionVersion.Should().Be("1.5.0");
        }

        [Fact]
        public void ConfigurationDataShouldNotIncludeAzureValuesWhenInAzureAndNotSafeToTrace()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };
            var aas = new AzureAppServices(new Dictionary<string, string>
            {
                // Without a DD_API_KEY, AAS does not consider it safe to trace
                // { ConfigurationKeys.ApiKey, "SomeValue" },
                { AzureAppServices.AzureAppServicesContextKey, "1" },
                { AzureAppServices.SiteExtensionVersionKey, "1.5.0" },
                { AzureAppServices.FunctionsExtensionVersionKey, "~3" },
            });

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, aas);

            var data = collector.GetConfigurationData();

            using var scope = new AssertionScope();
            data.AasConfigurationError.Should().BeTrue();
            data.CloudHosting.Should().Be("Azure");
            // TODO: Don't we want to collect these anyway? If so, need to update AzureAppServices behaviour
            data.AasAppType.Should().BeNullOrEmpty();
            data.AasFunctionsRuntimeVersion.Should().BeNullOrEmpty();
            data.AasSiteExtensionVersion.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ConfigurationDataShouldNotIncludeAzureValuesWhenNotInAzure()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, EmptyAasSettings);

            var data = collector.GetConfigurationData();

            using var scope = new AssertionScope();
            data.CloudHosting.Should().BeNullOrEmpty();
            data.AasAppType.Should().BeNullOrEmpty();
            data.AasFunctionsRuntimeVersion.Should().BeNullOrEmpty();
            data.AasSiteExtensionVersion.Should().BeNullOrEmpty();
        }
    }
}
