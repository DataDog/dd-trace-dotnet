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
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;

namespace Datadog.Trace.Tests.Telemetry
{
    public class ConfigurationTelemetryCollectorTests
    {
        private const string ServiceName = "serializer-test-app";

        [Fact]
        public void HasChangesAfterEachTracerSettingsAdded()
        {
            var settings = new ImmutableTracerSettings(new TracerSettings());

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(settings, ServiceName);

            collector.HasChanges().Should().BeTrue();
            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);
            data[ConfigTelemetryData.TracerInstanceCount] = 1;
            collector.HasChanges().Should().BeFalse();

            collector.RecordTracerSettings(settings, ServiceName);
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

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

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

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName);
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.AppSec.Enabled, enabled.ToString() },
            });
            collector.RecordSecuritySettings(new SecuritySettings(source, NullConfigurationTelemetry.Instance));

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            data[ConfigTelemetryData.SecurityEnabled].Should().Be(enabled);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConfigurationDataShouldIncludeExpectedIastValues(bool enabled)
        {
            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName);
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.Iast.Enabled, enabled.ToString() },
            });
            collector.RecordIastSettings(new IastSettings(source, NullConfigurationTelemetry.Instance));

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            data[ConfigTelemetryData.IastEnabled].Should().Be(enabled);
        }

        [Fact]
        public void ConfigurationDataShouldIncludeAzureValuesWhenInAzureAndSafeToTrace()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.ServiceName, ServiceName },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, serviceVersion },
                    { ConfigurationKeys.ApiKey, "SomeValue" },
                    { ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1" },
                    { ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, "1.5.0" },
                    { ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "~3" },
                });

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

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
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.ServiceName, ServiceName },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, serviceVersion },
                    // Without a DD_API_KEY, AAS does not consider it safe to trace
                    // { ConfigurationKeys.ApiKey, "SomeValue" },
                    { ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1" },
                    { ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, "1.5.0" },
                    { ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "~3" },
                });

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            using var scope = new AssertionScope();
            data[ConfigTelemetryData.AasConfigurationError].Should().BeOfType<bool>().Subject.Should().BeTrue();
            data[ConfigTelemetryData.CloudHosting].Should().Be("Azure");
            data[ConfigTelemetryData.AasAppType].Should().Be("function");
            data[ConfigTelemetryData.AasFunctionsRuntimeVersion].Should().Be("~3");
            data[ConfigTelemetryData.AasSiteExtensionVersion].Should().Be("1.5.0");
        }

        [Fact]
        public void ConfigurationDataShouldNotIncludeAzureValuesWhenNotInAzure()
        {
            const string env = "serializer-tests";
            const string serviceVersion = "1.2.3";
            var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

            var data = collector.GetConfigurationData()
                                .ToDictionary(x => x.Name, x => x.Value);

            using var scope = new AssertionScope();
            data.Should().NotContainKey(ConfigTelemetryData.CloudHosting);
            data.Should().NotContainKey(ConfigTelemetryData.AasAppType);
            data.Should().NotContainKey(ConfigTelemetryData.AasFunctionsRuntimeVersion);
            data.Should().NotContainKey(ConfigTelemetryData.AasSiteExtensionVersion);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ConfigurationDataShouldIncludeProfilerValues(bool profilerEnabled, bool codeHotspotsEnabled)
        {
            var collector = new ConfigurationTelemetryCollector();

            var status = new Mock<IProfilerStatus>();
            status.Setup(s => s.IsProfilerReady).Returns(profilerEnabled);

            var contextTracker = new Mock<IContextTracker>();
            contextTracker.Setup(s => s.IsEnabled).Returns(codeHotspotsEnabled);

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName);
            collector.RecordProfilerSettings(new Profiler(contextTracker.Object, status.Object));

            var data = collector.GetConfigurationData().ToDictionary(x => x.Name, x => x.Value);

            data[ConfigTelemetryData.ProfilerLoaded].Should().Be(profilerEnabled);
            data[ConfigTelemetryData.CodeHotspotsEnabled].Should().Be(codeHotspotsEnabled);
        }

        [Fact]
        public void ConfigurationDataShouldMarkAsManagedOnlyWhenProfilerNotAttached()
        {
            var collector = new ConfigurationTelemetryCollector();

            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), ServiceName);

            var data = collector.GetConfigurationData().ToDictionary(x => x.Name, x => x.Value);

            data[ConfigTelemetryData.NativeTracerVersion].Should().Be("None");
        }

#if NETFRAMEWORK
        [Fact]
        public void ConfigurationDataShouldIncludeExpectedFullTrustValues()
        {
            var carrier = new AppDomainCarrierClass();
            var data = carrier.BuildFullTrustConfigurationData();
            data[ConfigTelemetryData.FullTrustAppDomain].Should().Be(true);
        }

        public class AppDomainCarrierClass : MarshalByRefObject
        {
            public Dictionary<string, object> BuildFullTrustConfigurationData()
            {
                const string env = "serializer-tests";
                const string serviceVersion = "1.2.3";
                var settings = new TracerSettings() { ServiceName = ServiceName, Environment = env, ServiceVersion = serviceVersion };

                var collector = new ConfigurationTelemetryCollector();

                collector.RecordTracerSettings(new ImmutableTracerSettings(settings), ServiceName);

                var data = collector.GetConfigurationData()
                                    .ToDictionary(x => x.Name, x => x.Value);
                return data;
            }
        }
#endif
    }
}
