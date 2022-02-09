// <copyright file="ConfigurationTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
#if NETFRAMEWORK
using System.Security;
using System.Security.Permissions;
#endif
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
            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);
            data[ConfigTelemetryData.TracerInstanceCount] = 1;
            collector.HasChanges().Should().BeFalse();

            collector.RecordTracerSettings(settings, ServiceName, EmptyAasSettings);
            collector.HasChanges().Should().BeTrue();

            data = collector.GetConfigurationData().ToDictionary(x => x.Name, x => x.Value);
            data[ConfigTelemetryData.TracerInstanceCount] = 2;
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
        [InlineData(false)]
        [InlineData(true)]
        public void ConfigurationDataShouldIncludeExpectedSecurityValues(bool enabled)
        {
            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName, EmptyAasSettings);
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.AppSecEnabled, enabled.ToString() },
            });
            collector.RecordSecuritySettings(new SecuritySettings(source));

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            data[ConfigTelemetryData.SecurityEnabled].Should().Be(enabled);
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

            var data = collector.GetConfigurationData()
                .ToDictionary(x => x.Name, x => x.Value);

            using var scope = new AssertionScope();
            data[ConfigTelemetryData.AasConfigurationError].Should().BeOfType<bool>().Subject.Should().BeFalse();
            data[ConfigTelemetryData.CloudHosting].Should().Be("Azure");
            data[ConfigTelemetryData.AasAppType].Should().Be("function");
            data[ConfigTelemetryData.AasFunctionsRuntimeVersion].Should().Be("~3");
            data[ConfigTelemetryData.AasSiteExtensionVersion].Should().Be("1.5.0");
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

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            using var scope = new AssertionScope();
            data[ConfigTelemetryData.AasConfigurationError].Should().BeOfType<bool>().Subject.Should().BeTrue();
            data[ConfigTelemetryData.CloudHosting].Should().Be("Azure");
            // TODO: Don't we want to collect these anyway? If so, need to update AzureAppServices behaviour
            data[ConfigTelemetryData.AasAppType].Should().BeNull();
            data[ConfigTelemetryData.AasFunctionsRuntimeVersion].Should().BeNull();
            data[ConfigTelemetryData.AasSiteExtensionVersion].Should().BeNull();
        }

        [Fact]
        public void ConfigurationDataShouldNotIncludeAzureValuesWhenNotInAzure()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, EmptyAasSettings);

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            using var scope = new AssertionScope();
            data.Should().NotContainKey(ConfigTelemetryData.CloudHosting);
            data.Should().NotContainKey(ConfigTelemetryData.AasAppType);
            data.Should().NotContainKey(ConfigTelemetryData.AasFunctionsRuntimeVersion);
            data.Should().NotContainKey(ConfigTelemetryData.AasSiteExtensionVersion);
        }

#if NETFRAMEWORK
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConfigurationDataShouldIncludeExpectedFullTrustValues(bool isFullTrust)
        {
            Dictionary<string, object> data;
            if (isFullTrust)
            {
                var carrier = new AppDomainCarrierClass();
                data = carrier.BuildFullTrustConfigurationData();
            }
            else
            {
                PermissionSet permSet = new PermissionSet(PermissionState.None);
                permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
                permSet.AddPermission(new EnvironmentPermission(PermissionState.Unrestricted)); // The module initializer in the test DLL sets an environment variable to disable telemetry
                var remote = AppDomain.CreateDomain("ConfigurationDataShouldIncludeExpectedFullTrustValues", null, AppDomain.CurrentDomain.SetupInformation, permSet);

                var carrierType = typeof(AppDomainCarrierClass);
                var carrier = (AppDomainCarrierClass)remote.CreateInstanceAndUnwrap(carrierType.Assembly.FullName, carrierType.FullName);
                data = carrier.BuildFullTrustConfigurationData();
            }

            data[ConfigTelemetryData.FullTrustAppDomain].Should().Be(isFullTrust);
        }

        public class AppDomainCarrierClass : MarshalByRefObject
        {
            public Dictionary<string, object> BuildFullTrustConfigurationData()
            {
                const string env = "serializer-tests";
                const string serviceVersion = "1.2.3";
                var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

                var collector = new ConfigurationTelemetryCollector();

                collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName, EmptyAasSettings);

                var data = collector.GetConfigurationData()
                                    .ToDictionary(x => x.Name, x => x.Value);
                return data;
            }
        }
#endif
    }
}
