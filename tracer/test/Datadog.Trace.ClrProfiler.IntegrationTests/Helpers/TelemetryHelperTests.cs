// <copyright file="TelemetryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
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

    public TelemetryHelperTests()
    {
        _app = new ApplicationTelemetryData(
            "service",
            "env",
            TracerConstants.AssemblyVersion,
            "dotnet",
            FrameworkDescription.Instance.ProductVersion);
        _host = new HostTelemetryData();
    }

    [Fact]
    public void AssertIntegration_HandlesMultipleTelemtryPushes()
    {
        var collector = new IntegrationTelemetryCollector();
        var telemetryData = new List<TelemetryData>();

        collector.IntegrationRunning(IntegrationId.Aerospike);

        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        collector.IntegrationGeneratedSpan(IntegrationId.Aerospike);
        collector.IntegrationRunning(IntegrationId.Couchbase);
        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        collector.IntegrationRunning(IntegrationId.Kafka);
        collector.IntegrationGeneratedSpan(IntegrationId.Msmq);
        var tracerSettings = new TracerSettings(null, NullConfigurationTelemetry.Instance)
        {
            DisabledIntegrationNames = new HashSet<string> { nameof(IntegrationId.Kafka), nameof(IntegrationId.Msmq) }
        };

        collector.RecordTracerSettings(new(tracerSettings));
        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        // we must have an app closing
        telemetryData.Add(_dataBuilder.BuildAppClosingTelemetryData(_app, _host));

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
        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        if (errorIsFirstTelemetry)
        {
            collector.IntegrationRunning(IntegrationId.Grpc);
        }
        else
        {
            collector.IntegrationDisabledDueToError(IntegrationId.Grpc, "Some error");
        }

        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        collector.IntegrationRunning(IntegrationId.Npgsql);
        telemetryData.AddRange(BuildTelemetryData(collector.GetData()));

        // we must have an app closing
        telemetryData.Add(_dataBuilder.BuildAppClosingTelemetryData(_app, _host));

        var checkTelemetryFunc = () => TelemetryHelper.AssertIntegration(telemetryData, IntegrationId.Aerospike, enabled: true, autoEnabled: true);

        checkTelemetryFunc.Should().Throw<XunitException>();
    }

    private TelemetryData[] BuildTelemetryData(ICollection<IntegrationTelemetryData> integrations)
        => _dataBuilder.BuildTelemetryData(_app, _host, null, null, integrations);
}
