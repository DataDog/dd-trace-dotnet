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
        ManualInstrumentationConfigurationSourceBase manualConfigSource =
            useLegacySettings
                ? new ManualInstrumentationLegacyConfigurationSource(values, isFromDefaults)
                : new ManualInstrumentationConfigurationSource(values, isFromDefaults);

        IConfigurationSource source = isFromDefaults
                                          ? new CompositeConfigurationSource([manualConfigSource, GlobalConfigurationSource.Instance])
                                          : manualConfigSource;

        var settings = new TracerSettings(source, new ConfigurationTelemetry(), new OverrideErrorLog());

        // Update the global instance
        Trace.Tracer.ConfigureInternal(settings);
    }
}
