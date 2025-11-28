// <copyright file="EventTrackingSdkV2TrackUserLoginFailureUserIdIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec.EventTrackingSdkV2;

/// <summary>
/// System.Void Datadog.Trace.AppSec.EventTrackingSdkV2::TrackUserLoginFailure(string userLogin, bool exists, string? userId = null, IDictionary[string, string>? metadata = null) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.AppSec.EventTrackingSdkV2",
    MethodName = "TrackUserLoginFailure",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.Bool, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = "3.0.15",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EventTrackingSdkV2TrackUserLoginFailureUserIdIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string userLogin, bool exists, string? userId, IDictionary<string, string>? metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdkV2_TrackUserLoginFailure_UserId);
        Datadog.Trace.AppSec.EventTrackingSdkV2.TrackUserLoginFailure(userLogin, exists, userId is not null ? new UserDetails(userId) : null, metadata, Trace.Tracer.Instance);
        return CallTargetState.GetDefault();
    }
}
