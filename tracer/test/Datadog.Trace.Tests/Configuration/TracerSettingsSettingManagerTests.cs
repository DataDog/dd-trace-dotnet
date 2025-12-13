// <copyright file="TracerSettingsSettingManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;
using SettingChanges = Datadog.Trace.Configuration.TracerSettings.SettingsManager.SettingChanges;

namespace Datadog.Trace.Tests.Configuration;

public class TracerSettingsSettingManagerTests
{
    [Fact]
    public void UpdateSettings_HandlesDynamicConfigurationChanges()
    {
        var initialTelemetry = new ConfigurationTelemetry();
        var tracerSettings = new TracerSettings(source: null, initialTelemetry);
        SettingChanges settingChanges = null;
        tracerSettings.Manager.SubscribeToChanges(changes => settingChanges = changes);

        // default is null, but it's recorded as "1.0" in  telemetry
        tracerSettings.Manager.InitialMutableSettings.GlobalSamplingRate.Should().Be(null);
        var sampleRateConfig = GetLatestSampleRateTelemetry(initialTelemetry);

        sampleRateConfig.Should().NotBeNull();
        sampleRateConfig.Origin.Should().Be(ConfigurationOrigins.Default);
        sampleRateConfig.DoubleValue.Should().Be(1.0);

        var rawConfig1 = """{"action": "enable", "revision": 1698167126064, "service_target": {"service": "test_service", "env": "test_env"}, "lib_config": {"tracing_sampling_rate": 0.7, "log_injection_enabled": null, "tracing_header_tags": null, "runtime_metrics_enabled": null, "tracing_debug": null, "tracing_service_mapping": null, "tracing_sampling_rules": null, "data_streams_enabled": null, "dynamic_instrumentation_enabled": null, "exception_replay_enabled": null, "code_origin_enabled": null, "live_debugging_enabled": null}, "id": "-1796479631020605752"}""";
        var dynamicConfig = new DynamicConfigConfigurationSource(rawConfig1, ConfigurationOrigins.RemoteConfig);
        var telemetry = new ConfigurationTelemetry();

        var wasUpdated = tracerSettings.Manager.UpdateDynamicConfigurationSettings(
            dynamicConfig,
            centralTelemetry: telemetry);

        wasUpdated.Should().BeTrue();
        settingChanges.Should().NotBeNull();
        settingChanges!.UpdatedExporter.Should().BeNull();
        settingChanges!.UpdatedMutable.Should().NotBeNull();
        settingChanges!.UpdatedMutable.GlobalSamplingRate.Should().Be(0.7);

        sampleRateConfig = GetLatestSampleRateTelemetry(telemetry);

        sampleRateConfig.Should().NotBeNull();
        sampleRateConfig.Origin.Should().Be(ConfigurationOrigins.RemoteConfig);
        sampleRateConfig.DoubleValue.Should().Be(0.7);
        telemetry.Clear();

        // reset to "default"
        var rawConfig2 = """{"action": "enable", "revision": 1698167126064, "service_target": {"service": "test_service", "env": "test_env"}, "lib_config": {"tracing_sampling_rate": null, "log_injection_enabled": null, "tracing_header_tags": null, "runtime_metrics_enabled": null, "tracing_debug": null, "tracing_service_mapping": null, "tracing_sampling_rules": null, "data_streams_enabled": null, "dynamic_instrumentation_enabled": null, "exception_replay_enabled": null, "code_origin_enabled": null, "live_debugging_enabled": null}, "id": "5931732111467439992"}""";
        dynamicConfig = new DynamicConfigConfigurationSource(rawConfig2, ConfigurationOrigins.RemoteConfig);

        wasUpdated = tracerSettings.Manager.UpdateDynamicConfigurationSettings(
            dynamicConfig,
            centralTelemetry: telemetry);

        wasUpdated.Should().BeTrue();
        settingChanges.Should().NotBeNull();
        settingChanges!.UpdatedExporter.Should().BeNull();
        settingChanges!.UpdatedMutable.Should().NotBeNull();
        settingChanges!.UpdatedMutable.GlobalSamplingRate.Should().Be(null);

        sampleRateConfig = GetLatestSampleRateTelemetry(telemetry);

        sampleRateConfig.Should().NotBeNull();
        sampleRateConfig.Origin.Should().Be(ConfigurationOrigins.Default);
        sampleRateConfig.DoubleValue.Should().Be(1.0);
        telemetry.Clear();

        // Send the same config again, and make sure we _don't_ record more telemetry, so what we already have is correct
        var existingChanges = settingChanges;
        wasUpdated = tracerSettings.Manager.UpdateDynamicConfigurationSettings(
            dynamicConfig,
            centralTelemetry: telemetry);

        wasUpdated.Should().BeFalse();
        existingChanges.Should().Be(settingChanges);
        telemetry.GetQueueForTesting().Should().BeEmpty();
    }

    private static ConfigurationTelemetry.ConfigurationTelemetryEntry GetLatestSampleRateTelemetry(ConfigurationTelemetry telemetry)
    {
        return telemetry
              .GetQueueForTesting()
              .Where(x => x.Key == ConfigurationKeys.GlobalSamplingRate)
              .OrderByDescending(x => x.SeqId)
              .FirstOrDefault();
    }
}
