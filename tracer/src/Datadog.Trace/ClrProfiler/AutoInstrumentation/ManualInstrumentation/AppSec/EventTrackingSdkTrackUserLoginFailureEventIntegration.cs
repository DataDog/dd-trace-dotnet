// <copyright file="EventTrackingSdkTrackUserLoginFailureEventIntegration.cs" company="Datadog">
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
/// System.Void Datadog.Trace.AppSec.EventTrackingSdk::TrackUserLoginFailureEvent(System.String,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.AppSec.EventTrackingSdk",
    MethodName = "TrackUserLoginFailureEvent",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { ClrNames.String, ClrNames.Bool },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class EventTrackingSdkTrackUserLoginFailureEventIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string userId, bool exists)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackUserLoginFailureEvent);
        EventTrackingSdk.TrackUserLoginFailureEvent(userId, exists, null, Datadog.Trace.Tracer.Instance);
        return CallTargetState.GetDefault();
    }
}
