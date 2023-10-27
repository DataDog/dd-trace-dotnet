﻿// <copyright file="TelemetryHelperTests.cs" company="Datadog">
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
    private readonly ApplicationTelemetryDataV2 _appV2;
    private readonly HostTelemetryDataV2 _hostV2;
    private readonly TelemetryDataBuilderV2 _dataBuilderV2 = new();

    public TelemetryHelperTests()
    {
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
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData(), sendAppClosing: true));

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
        telemetryData.Add(BuildTelemetryDataV2(collector.GetData(), sendAppStarted: false, sendAppClosing: true));

        var checkTelemetryFunc = () => TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);

        checkTelemetryFunc.Should().Throw<XunitException>();
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
        telemetryData.Add(BuildTelemetryDataV2(null, collector.GetData(), sendAppStarted: false, sendAppClosing: true));

        using var s = new AssertionScope();
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, true);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled, false);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled, true);
    }

    private TelemetryWrapper BuildTelemetryDataV2(
        ICollection<IntegrationTelemetryData> integrations,
        ICollection<ConfigurationKeyValue> configuration = null,
        bool sendAppStarted = true,
        bool sendAppClosing = false)
        => new TelemetryWrapper.V2(
            _dataBuilderV2.BuildTelemetryData(
                _appV2,
                _hostV2,
                new TelemetryInput(configuration, null, integrations, null, null, sendAppStarted),
                namingSchemeVersion: "1",
                sendAppClosing));
}
