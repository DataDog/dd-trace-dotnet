// <copyright file="ConfigureIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Void Datadog.Trace.Tracer::Configure(System.Collections.Generic.Dictionary`2[System.String,System.Object]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "Configure",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = "3.7.0",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ConfigureIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigureIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget>(Dictionary<string, object?> values)
    {
        ConfigureSettingsWithManualOverrides(values, useLegacySettings: false);

        return CallTargetState.GetDefault();
    }

    internal static void ConfigureSettingsWithManualOverrides(Dictionary<string, object?> values, bool useLegacySettings)
    {
        // Is this from calling new TracerSettings() or TracerSettings.Global?
        var isFromDefaults = values.TryGetValue(TracerSettingKeyConstants.IsFromDefaultSourcesKey, out var value) && value is true;

        // Build the configuration sources, including our manual instrumentation values
        ManualInstrumentationConfigurationSourceBase manualConfig =
            useLegacySettings
                ? new ManualInstrumentationLegacyConfigurationSource(values, isFromDefaults)
                : new ManualInstrumentationConfigurationSource(values, isFromDefaults);

        var tracerSettings = Datadog.Trace.Tracer.Instance.Settings;
        var dynamicConfig = GlobalConfigurationSource.DynamicConfigurationSource;
        var initialSettings = isFromDefaults
                                  ? tracerSettings.MutableSettings
                                  : MutableSettings.CreateWithoutDefaultSources(tracerSettings);

        // TODO: these will eventually live elsewhere
        var currentSettings = tracerSettings.MutableSettings;

        var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
            dynamicConfig,
            manualConfig,
            initialSettings,
            tracerSettings,
            TelemetryFactory.Config,
            new OverrideErrorLog()); // TODO: We'll later report these

        if (currentSettings.Equals(newMutableSettings))
        {
            Log.Debug("No changes detected in the new configuration in code");
            return;
        }

        Log.Information("Applying new configuration in code");
        GlobalConfigurationSource.UpdateManualConfigurationSource(manualConfig);

        var newSettings = tracerSettings with { MutableSettings = newMutableSettings };

        // Update the global instance
        Trace.Tracer.ConfigureInternal(newSettings);
    }
}
