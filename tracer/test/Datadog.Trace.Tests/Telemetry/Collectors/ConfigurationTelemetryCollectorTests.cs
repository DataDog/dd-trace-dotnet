// <copyright file="ConfigurationTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using ConfigurationKeys = Datadog.Trace.Configuration.ConfigurationKeys;

namespace Datadog.Trace.Tests.Telemetry;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer("DD_INJECTION_ENABLED", "DD_INJECT_FORCE")]
public class ConfigurationTelemetryCollectorTests
{
    public static IEnumerable<object[]> GetPropagatorConfigurations()
        => from propagationStyleExtract in new string[] { null, "tracecontext" }
           from propagationStyleInject in new string[] { null, "datadog" }
           from propagationStyle in new string[] { null, "B3" }
           from activityListenerEnabled in new[] { "false", "true" }
           select new[] { propagationStyleExtract, propagationStyleInject, propagationStyle, activityListenerEnabled };

    [Fact]
    public void HasChangesAfterEachTracerSettingsAdded()
    {
        var collector = new ConfigurationTelemetry();

        var settings1 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "1.2.3" } }),
            collector,
            new OverrideErrorLog());

        collector.HasChanges().Should().BeTrue();
        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.ServiceVersion).Should().Be("1.2.3");

        collector.HasChanges().Should().BeFalse();
        var settings2 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "2.0.0" } }),
            collector,
            new OverrideErrorLog());

        collector.HasChanges().Should().BeTrue();
        GetLatestValueFromConfig(collector.GetData(), ConfigurationKeys.ServiceVersion).Should().Be("2.0.0");

        collector.HasChanges().Should().BeFalse();
    }

    [Fact]
    public void CopiedChangesHavePrecedence()
    {
        var collector = new ConfigurationTelemetry();
        var secondary = new ConfigurationTelemetry();

        var settings1 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "1.2.3" } }),
            secondary,
            new OverrideErrorLog());

        secondary.HasChanges().Should().BeTrue();
        collector.HasChanges().Should().BeFalse();

        // Using collector directly
        var settings2 = new TracerSettings(
            new NameValueConfigurationSource(
                new NameValueCollection { { ConfigurationKeys.ServiceVersion, "2.0.0" } }),
            collector,
            new OverrideErrorLog());

        collector.HasChanges().Should().BeTrue();

        // Merged changes should take precedence over existing
        secondary.CopyTo(collector);

        var data = collector.GetData();
        GetLatestValueFromConfig(data, ConfigurationKeys.ServiceVersion).Should().Be("1.2.3");
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
            { ConfigurationKeys.AzureAppService.SiteExtensionVersionKey, "1.5.0" },
            { PlatformKeys.AzureAppService.SiteNameKey, "site-name" },
            { PlatformKeys.AzureFunctions.FunctionsExtensionVersion, "~4" },
            { PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, "dotnet-isolated" },
            { ConfigurationKeys.ServiceName, serviceName },
            { ConfigurationKeys.Environment, env },
            { ConfigurationKeys.ServiceVersion, serviceVersion },
        };
        // Without a DD_API_KEY, AAS does not consider it safe to trace
        if (isSafeToTrace)
        {
            config.Add(ConfigurationKeys.ApiKey, "SomeValue");
        }

        _ = new TracerSettings(new NameValueConfigurationSource(config), collector, new OverrideErrorLog());

        var data = collector.GetData();

        using var scope = new AssertionScope();
        GetLatestValueFromConfig(data, ConfigurationKeys.AzureAppService.SiteExtensionVersionKey).Should().Be("1.5.0");
        GetLatestValueFromConfig(data, PlatformKeys.AzureAppService.SiteNameKey).Should().Be("site-name");
        GetLatestValueFromConfig(data, PlatformKeys.AzureFunctions.FunctionsExtensionVersion).Should().Be("~4");
        GetLatestValueFromConfig(data, PlatformKeys.AzureFunctions.FunctionsWorkerRuntime).Should().Be("dotnet-isolated");
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

        _ = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.ServiceVersion, "1.2.3" } }), collector, new OverrideErrorLog());

        collector.Clear();
        collector.GetData().Should().BeNull();
    }

    [Fact]
    public void ConfigurationDataShouldMarkAsManagedOnlyWhenProfilerNotAttached()
    {
        var collector = new ConfigurationTelemetry();

        var s = new TracerSettings(NullConfigurationSource.Instance, collector, new OverrideErrorLog());

        GetLatestValueFromConfig(collector.GetData(), ConfigTelemetryData.NativeTracerVersion).Should().Be("None");
    }

    [Theory]
    [MemberData(nameof(GetPropagatorConfigurations))]
    public void ConfigurationDataShouldIncludeExpectedPropagationValues(string propagationStyleExtract, string propagationStyleInject, string propagationStyle, string activityListenerEnabled)
    {
        var collector = new ConfigurationTelemetry();
        var config = new NameValueCollection
        {
            { ConfigurationKeys.PropagationStyleExtract, propagationStyleExtract },
            { ConfigurationKeys.PropagationStyleInject, propagationStyleInject },
            { ConfigurationKeys.PropagationStyle, propagationStyle },
            { ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, activityListenerEnabled },
        };

        _ = new TracerSettings(new NameValueConfigurationSource(config), collector, new OverrideErrorLog());

        var data = collector.GetData();

        using var scope = new AssertionScope();
        var (extractKey, extractValue) = (propagationStyleExtract, propagationStyle) switch
        {
            (not null, _) => (ConfigurationKeys.PropagationStyleExtract, propagationStyleExtract),
            (null, not null) => (ConfigurationKeys.PropagationStyle, propagationStyle),
            (null, null) => (ConfigurationKeys.PropagationStyleExtract, "Datadog,tracecontext,baggage"),
        };

        var (injectKey, injectValue) = (propagationStyleInject, propagationStyle) switch
        {
            (not null, _) => (ConfigurationKeys.PropagationStyleInject, propagationStyleInject),
            (null, not null) => (ConfigurationKeys.PropagationStyle, propagationStyle),
            (null, null) => (ConfigurationKeys.PropagationStyleInject, "Datadog,tracecontext,baggage"),
        };

        GetLatestValueFromConfig(data, extractKey).Should().Be(extractValue);
        GetLatestValueFromConfig(data, injectKey).Should().Be(injectValue);
    }

    [Fact]
    public void ConfigurationDataShouldReportDefaultValues()
    {
        var collector = new ConfigurationTelemetry();
        var source = new NameValueConfigurationSource(new NameValueCollection());

        _ = new TracerSettings(source, collector, new OverrideErrorLog());
        _ = new ProfilerSettings(source, source, collector);
        _ = new SecuritySettings(source, collector);

        var data = collector.GetData();

        GetLatestValueFromConfig(data, "DD_TRACE_ENABLED", ConfigurationOrigins.Default).Should().Be(true);
        var expected = ProfilerSettings.IsProfilingSupported ? "false" : null;
        GetLatestValueFromConfig(data, "DD_PROFILING_ENABLED", ConfigurationOrigins.Default).Should().Be(expected);
        GetLatestValueFromConfig(data, "DD_APPSEC_ENABLED", ConfigurationOrigins.Default).Should().Be(false);
        GetLatestValueFromConfig(data, "DD_DATA_STREAMS_ENABLED", ConfigurationOrigins.Default).Should().Be(true);
        GetLatestValueFromConfig(data, "DD_TAGS", ConfigurationOrigins.Default).Should().Be(string.Empty);
        GetLatestValueFromConfig(data, "DD_TRACE_HEADER_TAGS", ConfigurationOrigins.Default).Should().Be("[]");
        GetLatestValueFromConfig(data, "DD_LOGS_INJECTION", ConfigurationOrigins.Default).Should().Be(true);
        GetLatestValueFromConfig(data, "DD_TRACE_SAMPLE_RATE", ConfigurationOrigins.Default).Should().Be(1.0);
        GetLatestValueFromConfig(data, "instrumentation_source").Should().Be("manual");
    }

    [Fact]
    public void ConfigurationDataShouldReportSSIValues()
    {
        Environment.SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
        Environment.SetEnvironmentVariable("DD_INJECT_FORCE", "true");

        var collector = new ConfigurationTelemetry();
        var source = new NameValueConfigurationSource(new NameValueCollection());
        _ = new TracerSettings(source, collector, new OverrideErrorLog());
        _ = new SecuritySettings(source, collector);
        var data = collector.GetData();
        GetLatestValueFromConfig(data, ConfigTelemetryData.SsiInjectionEnabled).Should().Be("tracer");
        GetLatestValueFromConfig(data, ConfigTelemetryData.SsiAllowUnsupportedRuntimesEnabled).Should().Be("true");
        GetLatestValueFromConfig(data, ConfigTelemetryData.InstrumentationSource).Should().Be("ssi");
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

    private static object GetLatestValueFromConfig(ICollection<ConfigurationKeyValue> data, string key, ConfigurationOrigins? origin = null)
    {
        return data
              .Where(x => x.Name == key && (origin == null || x.Origin == origin.ToString().ToLowerInvariant()))
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
            var source = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.ServiceName, serviceName },
                { ConfigurationKeys.Environment, env },
                { ConfigurationKeys.ServiceVersion, serviceVersion },
            });
            var s = new TracerSettings(source, collector, new OverrideErrorLog());

            return GetLatestValueFromConfig(collector.GetData(), ConfigTelemetryData.FullTrustAppDomain);
        }
    }
#endif
}
