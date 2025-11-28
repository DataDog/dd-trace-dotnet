// <copyright file="EventTrackingSdkV2TrackUserLoginFailureUserDetailsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
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
    ParameterTypeNames = [ClrNames.String, ClrNames.Bool, "Datadog.Trace.UserDetails", "System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = "3.0.15",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventTrackingSdkV2TrackUserLoginFailureUserDetailsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TUserDetails>(ref string userLogin, ref bool exists, TUserDetails userDetails, ref IDictionary<string, string>? metadata)
        where TUserDetails : IUserDetails
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdkV2_TrackUserLoginFailure_UserDetails);
        Datadog.Trace.AppSec.EventTrackingSdkV2.TrackUserLoginFailure(userLogin, exists, userDetails, metadata, Trace.Tracer.Instance);
        return CallTargetState.GetDefault();
    }
}
