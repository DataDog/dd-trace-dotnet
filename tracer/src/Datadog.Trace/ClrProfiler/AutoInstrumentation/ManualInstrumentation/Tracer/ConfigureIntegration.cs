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
using Datadog.Trace.Telemetry.Metrics;

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
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Configure);

        // Is this from calling new TracerSettings() or TracerSettings.Global?
        var isFromDefaults = values.TryGetValue(TracerSettingKeyConstants.IsFromDefaultSourcesKey, out var value) && value is true;

        // Build the configuration sources, including our manual instrumentation values
        ManualInstrumentationConfigurationSourceBase manualConfig =
            useLegacySettings
                ? new ManualInstrumentationLegacyConfigurationSource(values, isFromDefaults)
                : new ManualInstrumentationConfigurationSource(values, isFromDefaults);

        // We need to save this immediately, even if there's no manifest changes in the final settings
        GlobalConfigurationSource.UpdateManualConfigurationSource(manualConfig);

        var tracerSettings = Datadog.Trace.Tracer.Instance.Settings;
        var dynamicConfig = GlobalConfigurationSource.DynamicConfigurationSource;
        var initialSettings = isFromDefaults
                                  ? tracerSettings.InitialMutableSettings
                                  : MutableSettings.CreateWithoutDefaultSources(tracerSettings);

        // TODO: these will eventually live elsewhere
        var currentSettings = tracerSettings.MutableSettings;

        var manualTelemetry = new ConfigurationTelemetry();
        var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
            dynamicConfig,
            manualConfig,
            initialSettings,
            tracerSettings,
            manualTelemetry,
            new OverrideErrorLog()); // TODO: We'll later report these

        var isSameMutableSettings = currentSettings.Equals(newMutableSettings);

        // The only exporter setting we currently _allow_ to change is the AgentUri, but if that does change,
        // it can mean that _everything_ about the exporter settings changes. To minimize the work to do, and
        // to simplify comparisons, we try to read the agent url from the manual setting. If it's missing, not
        // set, or unchanged, there's no need to update the exporter settings. In the future, ExporterSettings
        // will live separate from TracerSettings entirely.
        var exporterTelemetry = new ConfigurationTelemetry();
        var newRawExporterSettings = ExporterSettings.Raw.CreateUpdatedFromManualConfig(
            tracerSettings.Exporter.RawSettings,
            manualConfig,
            exporterTelemetry,
            isFromDefaults);
        var isSameExporterSettings = tracerSettings.Exporter.RawSettings.Equals(newRawExporterSettings);

        if (isSameMutableSettings && isSameExporterSettings)
        {
            Log.Debug("No changes detected in the new configuration in code");
            // Even though there were no "real" changes, there may be _effective_ changes in telemetry that
            // need to be recorded (e.g. the customer set the value in code but it was already set via
            // env vars). We _should_ record exporter settings too, but that introduces a bunch of complexity
            // which we'll resolve later anyway, so just have that gap for now (it's very niche).
            // If there are changes, they're recorded automatically in ConfigureInternal
            manualTelemetry.CopyTo(TelemetryFactory.Config);
            return;
        }

        Log.Information("Applying new configuration in code");
        TracerSettings newSettings;
        if (isSameExporterSettings)
        {
            newSettings = tracerSettings with { MutableSettings = newMutableSettings };
        }
        else
        {
            var exporterSettings = new ExporterSettings(newRawExporterSettings, exporterTelemetry);
            newSettings = isSameMutableSettings
                              ? tracerSettings with { Exporter = exporterSettings }
                              : tracerSettings with { MutableSettings = newMutableSettings, Exporter = exporterSettings };
        }

        // Update the global instance
        Trace.Tracer.Configure(newSettings);
    }
}
