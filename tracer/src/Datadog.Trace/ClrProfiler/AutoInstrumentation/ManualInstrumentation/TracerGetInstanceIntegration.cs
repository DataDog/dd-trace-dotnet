// <copyright file="TracerGetInstanceIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

/// <summary>
/// Datadog.Trace.Tracer Datadog.Trace.Tracer::get_Instance() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "get_Instance",
    ReturnTypeName = "Datadog.Trace.Tracer",
    ParameterTypeNames = [],
    MinimumVersion = "3.0.0",
    MaximumVersion = "*.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TracerGetInstanceIntegration
{
    private static bool _hasWrittenWarning;

    internal static CallTargetState OnMethodBegin<TTarget>()
    {
        // This integration is solely so that we get telemetry of where customers are using a v3+ version of
        // custom instrumentation, with an unsupported version (v2) of the automatic instrumentation.
        // This integration is removed in v3 of the automatic instrumentation.
        if (!_hasWrittenWarning)
        {
            try
            {
                _hasWrittenWarning = true;
                var manualVersion = typeof(TTarget).Assembly().GetName().Version;
                DatadogLogging
                   .GetLoggerFor<TracerGetInstanceIntegration>()
                   .Warning(
                        "The version of the Datadog.Trace NuGet package '{ManualVersion}' is not supported by the current automatic instrumentation version '{AutomaticVersion}'." +
                        "Please update your automatic instrumentation installation to the latest version",
                        manualVersion?.ToString(3) ?? "3.x.x",
                        TracerConstants.AssemblyVersion);
                TelemetryFactory.Metrics.RecordCountUnsupportedCustomInstrumentationServices();
            }
            catch
            {
                // Just swallow if something went wrong retrieving the type etc
            }
        }

        return CallTargetState.GetDefault();
    }
}
