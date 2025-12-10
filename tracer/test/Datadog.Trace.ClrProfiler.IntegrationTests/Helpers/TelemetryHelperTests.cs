// <copyright file="TelemetryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class TelemetryHelperTests
{
    private readonly ApplicationTelemetryData _app;
    private readonly HostTelemetryData _host;
    private readonly TelemetryDataBuilder _dataBuilder = new();
    private readonly ITestOutputHelper _output;

    public TelemetryHelperTests(ITestOutputHelper output)
    {
        _app = new ApplicationTelemetryData(
            "service",
            "env",
            "1.2.3",
            TracerConstants.AssemblyVersion,
            "dotnet",
            FrameworkDescription.Instance.ProductVersion,
            runtimeName: "dotnet",
            runtimeVersion: "7.0.1",
            commitSha: "testCommitSha",
            repositoryUrl: "testRepositoryUrl",
            processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads");
        _host = new HostTelemetryData("MY_HOST", "Windows", "x64");
        _output = output;
    }

    [Fact]
    public void AssertIntegration_HandlesMultipleTelemetryPushes()
    {
        var collector = new IntegrationTelemetryCollector();
        var metricsCollector = new MetricsTelemetryCollector();
        var telemetryData = new List<TelemetryData>();

        collector.IntegrationRunning(IntegrationId.Aerospike);

        metricsCollector.AggregateMetrics();
        telemetryData.Add(BuildTelemetryData(collector.GetData(), metrics: metricsCollector.GetMetrics()));

        // The updates to both the IntegrationTelemetryCollector and the MetricsTelemetryCollector
        // are typically handled by TelemetryController.IntegrationGeneratedSpan(IntegrationId),
        // so we simulate that here with the separate calls
        collector.IntegrationGeneratedSpan(IntegrationId.Aerospike);
        metricsCollector.RecordCountSpanCreated(IntegrationId.Aerospike.GetMetricTag());

        collector.IntegrationRunning(IntegrationId.Couchbase);

        metricsCollector.AggregateMetrics();
        telemetryData.Add(BuildTelemetryData(collector.GetData(), metrics: metricsCollector.GetMetrics(), sendAppStarted: false));

        collector.IntegrationRunning(IntegrationId.Kafka);
        collector.IntegrationRunning(IntegrationId.Msmq);
        var tracerSettings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.DisabledIntegrations, $"{nameof(IntegrationId.Kafka)};{nameof(IntegrationId.Msmq)}" }
        });

        collector.RecordTracerSettings(tracerSettings.Manager.InitialMutableSettings);
        metricsCollector.AggregateMetrics();
        telemetryData.Add(BuildTelemetryData(collector.GetData(), metrics: metricsCollector.GetMetrics(), sendAppClosing: true));

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
    public void AssertIntegration_ErrorsWhenIntegrationError(bool errorIsFirstTelemetry)
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryData>();

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }
        else
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }

        collector.IntegrationRunning(IntegrationId.Aerospike);
        telemetryData.Add(BuildTelemetryData(collector.GetData()));

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }
        else
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }

        telemetryData.Add(BuildTelemetryData(collector.GetData(), sendAppStarted: false));

        collector.IntegrationRunning(IntegrationId.Npgsql);
        telemetryData.Add(BuildTelemetryData(collector.GetData(), sendAppStarted: false, sendAppClosing: true));

        var checkTelemetryFunc = () => TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);

        checkTelemetryFunc.Should().Throw<XunitException>();
    }

    [Fact]
    public void AssertConfiguration_HandlesMultipleTelemetryPushes()
    {
        var collector = new ConfigurationTelemetry();
        var telemetryData = new List<TelemetryData>();

        var config = new NameValueConfigurationSource(new NameValueCollection()
        {
            { ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "1" },
            { ConfigurationKeys.TraceEnabled, "0" },
            { ConfigurationKeys.AppSec.Enabled, "1" },
            { ConfigurationKeys.AppSec.ScaEnabled, "1" },
        });

        _ = new TracerSettings(config, collector, new OverrideErrorLog());

        telemetryData.Add(BuildTelemetryData(null, collector.GetData()));

        _ = new SecuritySettings(config, collector);
        telemetryData.Add(BuildTelemetryData(null, collector.GetData(), sendAppStarted: false, sendAppClosing: true));

        using var s = new AssertionScope();
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, true);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.TraceEnabled, false);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.Enabled, true);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.ScaEnabled);
        TelemetryHelper.AssertConfiguration(telemetryData, ConfigurationKeys.AppSec.ScaEnabled, true);
    }

    private TelemetryData BuildTelemetryData(
        ICollection<IntegrationTelemetryData> integrations,
        ICollection<ConfigurationKeyValue> configuration = null,
        MetricResults? metrics = null,
        bool sendAppStarted = true,
        bool sendAppClosing = false)
        => _dataBuilder.BuildTelemetryData(
                _app,
                _host,
                new TelemetryInput(configuration, null, integrations, null, metrics, null, sendAppStarted),
                namingSchemeVersion: "1",
                sendAppClosing);
}
