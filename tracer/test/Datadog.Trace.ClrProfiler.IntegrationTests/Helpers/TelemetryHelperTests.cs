// <copyright file="TelemetryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class TelemetryHelperTests
{
    private readonly ApplicationTelemetryData _app;
    private readonly HostTelemetryData _host;
    private readonly TelemetryDataBuilder _dataBuilder = new();
    private readonly ApplicationTelemetryDataV2 _appV2;
    private readonly HostTelemetryDataV2 _hostV2;
    private readonly TelemetryDataBuilderV2 _dataBuilderV2 = new();

    public TelemetryHelperTests()
    {
        _app = new ApplicationTelemetryData(
            "service",
            "env",
            TracerConstants.AssemblyVersion,
            "dotnet",
            FrameworkDescription.Instance.ProductVersion);
        _host = new HostTelemetryData();
        _appV2 = new ApplicationTelemetryDataV2(
            "service",
            "env",
            "1.2.3",
            TracerConstants.AssemblyVersion,
            "dotnet",
            FrameworkDescription.Instance.ProductVersion,
            runtimeName: "dotnet",
            runtimeVersion: "7.0.1");
        _hostV2 = new HostTelemetryDataV2("MY_HOST", "Windows", "x64");
    }

    [Fact]
    public void AssertIntegration_HandlesMultipleTelemetryPushes_v1()
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryWrapper>();

        collector.IntegrationRunning(IntegrationId.Aerospike);

        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        collector.IntegrationGeneratedSpan(IntegrationId.Aerospike);
        collector.IntegrationRunning(IntegrationId.Couchbase);
        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        collector.IntegrationRunning(IntegrationId.Kafka);
        collector.IntegrationGeneratedSpan(IntegrationId.Msmq);
        var tracerSettings = new TracerSettings(null, NullConfigurationTelemetry.Instance)
        {
            DisabledIntegrationNames = new HashSet<string> { nameof(IntegrationId.Kafka), nameof(IntegrationId.Msmq) }
        };

        collector.RecordTracerSettings(new(tracerSettings));
        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V1(_dataBuilder.BuildAppClosingTelemetryData(_app, _host)));

        using var s = new AssertionScope();
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Couchbase, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Kafka, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Msmq, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Npgsql, enabled: false, autoEnabled: false);
    }

    [Fact]
    public void AssertIntegration_HandlesMultipleTelemetryPushes_v2()
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryWrapper>();

        collector.IntegrationRunning(IntegrationId.Aerospike);

        telemetryData.Add(BuildTelemetryDataV2(collector.GetData()));

        collector.IntegrationGeneratedSpan(IntegrationId.Aerospike);
        collector.IntegrationRunning(IntegrationId.Couchbase);
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData(), sendAppStarted: false));

        collector.IntegrationRunning(IntegrationId.Kafka);
        collector.IntegrationGeneratedSpan(IntegrationId.Msmq);
        var tracerSettings = new TracerSettings(null, NullConfigurationTelemetry.Instance)
        {
            DisabledIntegrationNames = new HashSet<string> { nameof(IntegrationId.Kafka), nameof(IntegrationId.Msmq) }
        };

        collector.RecordTracerSettings(new(tracerSettings));
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData()));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V2(_dataBuilderV2.BuildAppClosingTelemetryData(_appV2, _hostV2, "1")));

        using var s = new AssertionScope();
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Couchbase, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Kafka, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Msmq, enabled: false, autoEnabled: true);
        TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Npgsql, enabled: false, autoEnabled: false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssertIntegration_ErrorsWhenIntegrationError_V1(bool errorIsFirstTelemetry)
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryWrapper>();

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }
        else
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }

        collector.IntegrationRunning(IntegrationId.Aerospike);
        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }
        else
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }

        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        collector.IntegrationRunning(IntegrationId.Npgsql);
        telemetryData.AddRange(BuildTelemetryDataV1(collector.GetData()));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V1(_dataBuilder.BuildAppClosingTelemetryData(_app, _host)));

        var checkTelemetryFunc = () => TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);

        checkTelemetryFunc.Should().Throw<XunitException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssertIntegration_ErrorsWhenIntegrationError_V2(bool errorIsFirstTelemetry)
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryWrapper>();

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }
        else
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }

        collector.IntegrationRunning(IntegrationId.Aerospike);
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData()));

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }
        else
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }

        telemetryData.Add(BuildTelemetryDataV2(collector.GetData(), sendAppStarted: false));

        collector.IntegrationRunning(IntegrationId.Npgsql);
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData(), sendAppStarted: false));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V2(_dataBuilderV2.BuildAppClosingTelemetryData(_appV2, _hostV2, "1")));

        var checkTelemetryFunc = () => TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);

        checkTelemetryFunc.Should().Throw<XunitException>();
    }

    [Fact]
    public void AssertConfiguration_HandlesMultipleTelemetryPushes_v1()
    {
        var collector = new ConfigurationTelemetryCollector();
        var telemetryData = new List<TelemetryWrapper>();

        var config = new NameValueConfigurationSource(new NameValueCollection()
        {
            { ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "1" },
            { ConfigurationKeys.TraceEnabled, "0" },
            { ConfigurationKeys.AppSec.Enabled, "1" },
        });

        collector.RecordTracerSettings(
            new ImmutableTracerSettings(new TracerSettings(config, NullConfigurationTelemetry.Instance)),
            "my-service");

        telemetryData.AddRange(BuildTelemetryDataV1(null, collector.GetConfigurationData()));

        collector.RecordSecuritySettings(new SecuritySettings(config, NullConfigurationTelemetry.Instance));
        telemetryData.AddRange(BuildTelemetryDataV1(null, collector.GetConfigurationData()));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V1(_dataBuilder.BuildAppClosingTelemetryData(_app, _host)));

        using var s = new AssertionScope();
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.RoutetemplateResourcenamesEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.RoutetemplateResourcenamesEnabled, true);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.Enabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.Enabled, false);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.SecurityEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigTelemetryData.SecurityEnabled, true);
    }

    [Fact]
    public void AssertConfiguration_HandlesMultipleTelemetryPushes_v2()
    {
        var collector = new ConfigurationTelemetry();
        var telemetryData = new List<TelemetryWrapper>();

        var config = new NameValueConfigurationSource(new NameValueCollection()
        {
            { ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "1" },
            { ConfigurationKeys.TraceEnabled, "0" },
            { ConfigurationKeys.AppSec.Enabled, "1" },
        });

        _ = new ImmutableTracerSettings(new TracerSettings(config, collector));

        telemetryData.Add(BuildTelemetryDataV2(null, collector.GetData()));

        _ = new SecuritySettings(config, collector);
        telemetryData.Add(BuildTelemetryDataV2(null, collector.GetData(), sendAppStarted: false));

        // we must have an app closing
        telemetryData.Add(new TelemetryWrapper.V2(_dataBuilderV2.BuildAppClosingTelemetryData(_appV2, _hostV2, "1")));

        using var s = new AssertionScope();
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, true);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled, false);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled, true);
    }

    private List<TelemetryWrapper> BuildTelemetryDataV1(
        ICollection<IntegrationTelemetryData> integrations,
        ICollection<TelemetryValue> configuration = null)
        => _dataBuilder.BuildTelemetryData(_app, _host, configuration, null, integrations)
                       .Select(x => (TelemetryWrapper)new TelemetryWrapper.V1(x))
                       .ToList();

    private TelemetryWrapper BuildTelemetryDataV2(
        ICollection<IntegrationTelemetryData> integrations,
        ICollection<ConfigurationKeyValue> configuration = null,
        bool sendAppStarted = true)
        => new TelemetryWrapper.V2(
            _dataBuilderV2.BuildTelemetryData(
                _appV2,
                _hostV2,
                new TelemetryInput(configuration, null, integrations, null, null, null),
                sendAppStarted,
                namingSchemeVersion: "1"));
}
