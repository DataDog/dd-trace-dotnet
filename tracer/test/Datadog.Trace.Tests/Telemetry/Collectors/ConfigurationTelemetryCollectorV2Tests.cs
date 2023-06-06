// <copyright file="ConfigurationTelemetryCollectorV2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;

namespace Datadog.Trace.Tests.Telemetry;

public class ConfigurationTelemetryCollectorV2Tests
{
    [Fact]
    public void HasChangesAfterEachTracerSettingsAdded()
    {
        var collector = new ConfigurationTelemetry();

        var settings1 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "1.2.3" } }),
            collector);

        collector.HasChanges().Should().BeTrue();
        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.ServiceVersion).Should().Be("1.2.3");

        collector.HasChanges().Should().BeFalse();
        var settings2 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "2.0.0" } }),
            collector);

        collector.HasChanges().Should().BeTrue();
        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.ServiceVersion).Should().Be("2.0.0");

        collector.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigurationDataShouldIncludeExpectedSecurityValues(bool enabled)
    {
        var collector = new ConfigurationTelemetry();

        new SecuritySettings(
            new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.AppSec.Enabled, enabled.ToString() }, }),
            collector);

        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.AppSec.Enabled).Should().Be(enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigurationDataShouldIncludeExpectedIastValues(bool enabled)
    {
        var collector = new ConfigurationTelemetry();

        new IastSettings(
            new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.Iast.Enabled, enabled.ToString() }, }),
            collector);

        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.Iast.Enabled).Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigurationDataShouldIncludeAzureValues(bool isSafeToTrace)
    {
        const string env = "serializer-tests";
        const string serviceName = "my-tests";
        const string serviceVersion = "1.2.3";
        var collector = new ConfigurationTelemetry();
        var config = new NameValueCollection
        {
            { ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "1" },
            { ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, "1.5.0" },
            { ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey, "~3" },
            { ConfigurationKeys.ServiceName, serviceName },
            { ConfigurationKeys.Environment, env },
            { ConfigurationKeys.ServiceVersion, serviceVersion },
        };
        // Without a DD_API_KEY, AAS does not consider it safe to trace
        if (isSafeToTrace)
        {
            config.Add(ConfigurationKeys.ApiKey, "SomeValue");
        }

        var settings = new ImmutableTracerSettings(new TracerSettings(new NameValueConfigurationSource(config), collector));

        var data = collector.GetData();

        using var scope = new AssertionScope();
        GetLatestValueFromConfig(data, ConfigurationKeys.AzureAppService.AzureAppServicesContextKey).Should().BeOfType<bool>().Subject.Should().BeTrue();
        GetLatestValueFromConfig(data, ConfigurationKeys.AzureAppService.SiteExtensionVersionKey).Should().Be("1.5.0");
        GetLatestValueFromConfig(data, ConfigurationKeys.AzureAppService.FunctionsExtensionVersionKey).Should().Be("~3");
        GetLatestValueFromConfig(data, ConfigTelemetryData.AasConfigurationError).Should().BeOfType<bool>().Subject.Should().Be(!isSafeToTrace);
        GetLatestValueFromConfig(data, ConfigTelemetryData.CloudHosting).Should().Be("Azure");
        GetLatestValueFromConfig(data, ConfigTelemetryData.AasAppType).Should().Be("function");
        if (isSafeToTrace)
        {
            GetLatestValueFromConfig(data, ConfigurationKeys.ApiKey).Should().Be("<redacted>");
        }
        else
        {
            GetLatestValueFromConfig(data, ConfigurationKeys.ApiKey).Should().BeNull();
        }
    }

    [Fact]
    public void HasNoDataAfterCallingClear()
    {
        var collector = new ConfigurationTelemetry();

        _ = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.ServiceVersion, "1.2.3" } }), collector);

        collector.Clear();
        collector.GetData().Should().BeNull();
    }

    [Fact]
    public void ConfigurationDataShouldMarkAsManagedOnlyWhenProfilerNotAttached()
    {
        var collector = new ConfigurationTelemetry();

        var s = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, collector));

        GetLatestValueFromConfig(collector.GetData(), ConfigTelemetryData.NativeTracerVersion).Should().Be("None");
    }

#if NETFRAMEWORK
    [Fact]
    public void ConfigurationDataShouldIncludeExpectedFullTrustValues()
    {
        var carrier = new AppDomainCarrierClass();
        var data = carrier.BuildFullTrustConfigurationData();
        data.Should().Be(true);
    }
#endif

    private static object GetLatestValueFromConfig(ICollection<ConfigurationKeyValue> data, string key)
    {
        return data
              .Where(x => x.Name == key)
              .OrderByDescending(x => x.SeqId)
              .FirstOrDefault()
              .Value;
    }
#if NETFRAMEWORK

    public class AppDomainCarrierClass : System.MarshalByRefObject
    {
        public object BuildFullTrustConfigurationData()
        {
            const string env = "serializer-tests";
            const string serviceName = "my-tests";
            const string serviceVersion = "1.2.3";
            var collector = new ConfigurationTelemetry();
            var s = new TracerSettings(NullConfigurationSource.Instance, collector)
            {
                ServiceName = serviceName,
                Environment = env,
                ServiceVersion = serviceVersion
            };

            return GetLatestValueFromConfig(collector.GetData(), ConfigTelemetryData.FullTrustAppDomain);
        }
    }
#endif
}
