// <copyright file="EventTrackingSdkTrackCustomEventMetadataIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec;

/// <summary>
/// System.Void Datadog.Trace.AppSec.EventTrackingSdk::TrackCustomEvent(System.String,System.Collections.Generic.IDictionary`2[System.String,System.String]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.AppSec.EventTrackingSdk",
    MethodName = "TrackCustomEvent",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.String]" },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class EventTrackingSdkTrackCustomEventMetadataIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string eventName, System.Collections.Generic.IDictionary<string, string> metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackCustomEvent_Metadata);
        EventTrackingSdk.TrackCustomEvent(eventName, metadata, Datadog.Trace.Tracer.Instance);
        return CallTargetState.GetDefault();
    }
}
