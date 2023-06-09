// <copyright file="TelemetryFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.Tests.Telemetry;

[CollectionDefinition(nameof(TelemetryFactoryTests), DisableParallelization = true)]
[Collection(nameof(TelemetryFactoryTests))]
[TelemetryRestorer]
public class TelemetryFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TelemetryFactory_DisabledIfTelemetryIsDisabled(bool v2Enabled)
    {
        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: false, // explicitly disabled
            configurationError: null,
            agentlessSettings: null,
            agentProxyEnabled: true,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: v2Enabled,
            metricsEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        controller.Should().Be(NullTelemetryController.Instance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TelemetryFactory_DisabledIfNoTransports(bool v2Enabled)
    {
        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: null, // no agentless
            agentProxyEnabled: false, // disable proxy
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: v2Enabled,
            metricsEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        controller.Should().Be(NullTelemetryController.Instance);
    }

    [Fact]
    public void TelemetryFactory_UsesV1ControllerIfV2Disabled()
    {
        // set the defaults (module initializer resets everything by default
        TelemetryFactory.SetConfigForTesting(new ConfigurationTelemetry());
        TelemetryFactory.SetMetricsForTesting(new MetricsTelemetryCollector());

        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: null,
            agentProxyEnabled: true,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: false,
            metricsEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        controller.Should().BeOfType<TelemetryController>();
        TelemetryFactory.Config.Should().BeOfType<NullConfigurationTelemetry>();
        TelemetryFactory.Metrics.Should().BeOfType<NullMetricsTelemetryCollector>();
    }

    [Fact]
    public void TelemetryFactory_UsesV2ControllerIfV2Enabled()
    {
        // set the defaults (module initializer resets everything by default
        TelemetryFactory.SetConfigForTesting(new ConfigurationTelemetry());
        TelemetryFactory.SetMetricsForTesting(new MetricsTelemetryCollector());

        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: null,
            agentProxyEnabled: true,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: true,
            metricsEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        controller.Should().BeOfType<TelemetryControllerV2>();
        TelemetryFactory.Config.Should().BeOfType<ConfigurationTelemetry>();
    }

    [Fact]
    public void TelemetryFactory_V2Telemetry_DisablesMetricsIfMetricsDisabled()
    {
        // set the defaults (module initializer resets everything by default
        TelemetryFactory.SetConfigForTesting(new ConfigurationTelemetry());
        TelemetryFactory.SetMetricsForTesting(new MetricsTelemetryCollector());

        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: null,
            agentProxyEnabled: true,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: true,
            metricsEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        TelemetryFactory.Metrics.Should().BeOfType<NullMetricsTelemetryCollector>();
    }

    [Fact]
    public void TelemetryFactory_V2Telemetry_EnablesMetricsIfMetricsEnabled()
    {
        // set the defaults (module initializer resets everything by default
        TelemetryFactory.SetConfigForTesting(new ConfigurationTelemetry());
        TelemetryFactory.SetMetricsForTesting(new MetricsTelemetryCollector());

        var factory = TelemetryFactory.CreateFactory();
        var tracerSettings = new ImmutableTracerSettings(new TracerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance));
        var settings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: null,
            agentProxyEnabled: true,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: true,
            metricsEnabled: true);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        TelemetryFactory.Metrics.Should().BeOfType<MetricsTelemetryCollector>();
    }
}
