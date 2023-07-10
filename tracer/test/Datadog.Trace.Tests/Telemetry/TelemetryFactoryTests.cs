﻿// <copyright file="TelemetryFactoryTests.cs" company="Datadog">
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
            metricsEnabled: false,
            debugEnabled: false);

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
            metricsEnabled: false,
            debugEnabled: false);

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
            metricsEnabled: false,
            debugEnabled: false);

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
            metricsEnabled: false,
            debugEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        controller.Should().BeOfType<TelemetryControllerV2>();
        TelemetryFactory.Config.Should().BeOfType<ConfigurationTelemetry>();
    }

    [Fact]
    public void TelemetryFactory_V1Telemetry_CollectorsPersistWhenNewController()
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
            metricsEnabled: false,
            debugEnabled: false);

        var controller1 = factory.CreateTelemetryController(tracerSettings, settings);
        var metrics1 = TelemetryFactory.Metrics;
        var config1 = TelemetryFactory.Metrics;

        var controller2 = factory.CreateTelemetryController(tracerSettings, settings);
        var metrics2 = TelemetryFactory.Metrics;
        var config2 = TelemetryFactory.Metrics;

        var v1Controller1 = controller1.Should().BeOfType<TelemetryController>().Subject;
        var v1Controller2 = controller2.Should().BeOfType<TelemetryController>().Subject;
        v1Controller1.Should().NotBe(v1Controller2);

        metrics1.Should().Be(metrics2);
        config1.Should().Be(config2);

        var dependencies = GetField<TelemetryController>("_dependencies");
        dependencies.GetValue(controller1).Should().BeSameAs(dependencies.GetValue(controller2));

        var integrations = GetField<TelemetryController>("_integrations");
        integrations.GetValue(controller1).Should().BeSameAs(integrations.GetValue(controller2));
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
            metricsEnabled: false,
            debugEnabled: false);

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
            metricsEnabled: true,
            debugEnabled: false);

        var controller = factory.CreateTelemetryController(tracerSettings, settings);

        TelemetryFactory.Metrics.Should().BeOfType<MetricsTelemetryCollector>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TelemetryFactory_V2Telemetry_ControllerAndCollectorsPersistWhenNewController(bool dependencyCollectionEnabled)
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
            dependencyCollectionEnabled: dependencyCollectionEnabled,
            v2Enabled: true,
            metricsEnabled: true,
            debugEnabled: false);

        // First controller
        var controller1 = factory.CreateTelemetryController(tracerSettings, settings);
        var metrics1 = TelemetryFactory.Metrics;
        var config1 = TelemetryFactory.Config;

        var dependencies = GetField<TelemetryControllerV2>("_dependencies");
        var dependencies1 = dependencies.GetValue(controller1);

        var integrations = GetField<TelemetryControllerV2>("_integrations");
        var integrations1 = integrations.GetValue(controller1);

        var products = GetField<TelemetryControllerV2>("_products");
        var products1 = products.GetValue(controller1);

        var application = GetField<TelemetryControllerV2>("_application");
        var application1 = application.GetValue(controller1);

        var v1Controller1 = controller1.Should().BeOfType<TelemetryControllerV2>().Subject;

        // Second controller
        var controller2 = factory.CreateTelemetryController(tracerSettings, settings);
        var metrics2 = TelemetryFactory.Metrics;
        var config2 = TelemetryFactory.Config;

        var v1Controller2 = controller2.Should().BeOfType<TelemetryControllerV2>().Subject;
        v1Controller1.Should().Be(v1Controller2);

        metrics1.Should().Be(metrics2);
        config1.Should().Be(config2);
        dependencies1.Should().BeSameAs(dependencies.GetValue(controller2));
        integrations1.Should().BeSameAs(integrations.GetValue(controller2));
        products1.Should().BeSameAs(products.GetValue(controller2));
        application1.Should().BeSameAs(application.GetValue(controller2));
    }

    private static FieldInfo GetField<T>(string name)
    {
        return typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
