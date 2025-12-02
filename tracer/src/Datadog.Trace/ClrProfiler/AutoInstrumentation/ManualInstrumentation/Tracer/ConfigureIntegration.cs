// <copyright file="ConfigureIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
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
public sealed class ConfigureIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ConfigureIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(Dictionary<string, object?> values)
    {
        ConfigureSettingsWithManualOverrides(values, useLegacySettings: false);

        return CallTargetState.GetDefault();
    }

    internal static void ConfigureSettingsWithManualOverrides(Dictionary<string, object?> values, bool useLegacySettings)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Configure);

        // There's an edge case in our APIs where if a user changes the agent URI to UDS on Windows
        // then we can no longer use the trace exporter, but this is something that we assume is
        // immutable today. To work around this edge case, we block updating the exporter settings to
        // point to UDS when you're running on Windows. Note that it's fine to set to UDS _initially_,
        // it's only _updating_ it to UDS on Windows that we block
        if (FrameworkDescription.Instance.IsWindows()
         && values.TryGetValue(TracerSettingKeyConstants.AgentUriKey, out var raw)
         && raw is Uri uri
         && uri.OriginalString.StartsWith(ExporterSettings.UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            InvalidConfigurationException.Throw(
                $"Error changing AgentUri. " +
                "AgentUri can not be set to a UDS endpoint in code when running on Windows. " +
                "If you need to use UDS on Windows, set the environment variable DD_TRACE_AGENT_URL instead to " +
                "ensure the app starts with the correct configuration");
        }

        // Is this from calling new TracerSettings() or TracerSettings.Global?
        var isFromDefaults = values.TryGetValue(TracerSettingKeyConstants.IsFromDefaultSourcesKey, out var value) && value is true;

        // Build the configuration sources, including our manual instrumentation values
        ManualInstrumentationConfigurationSourceBase manualConfig =
            useLegacySettings
                ? new ManualInstrumentationLegacyConfigurationSource(values, isFromDefaults)
                : new ManualInstrumentationConfigurationSource(values, isFromDefaults);

        var wasUpdated = Datadog.Trace.Tracer.Instance.Settings.Manager.UpdateManualConfigurationSettings(manualConfig, TelemetryFactory.Config);
        if (wasUpdated)
        {
            Log.Information("Setting updates made via configuration in code were applied");
        }
    }
}
