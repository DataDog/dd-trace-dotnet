// <copyright file="EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec.EventTrackingSdkV2;

/// <summary>
/// System.Void Datadog.Trace.AppSec.EventTrackingSdkV2::TrackUserLoginSuccess(string userLogin, string? userId = null, IDictionary[string, string]? metadata = null) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.AppSec.EventTrackingSdkV2",
    MethodName = "TrackUserLoginSuccess",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.String]"],
    MinimumVersion = "3.15.0",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string userLogin, string? userId, IDictionary<string, string>? metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdkV2_TrackUserLoginSuccess_UserId);
        if (userId is not null)
        {
            Datadog.Trace.AppSec.EventTrackingSdkV2.TrackUserLoginSuccess(userLogin,  new UserDetails(userId), metadata, Trace.Tracer.Instance);
        }
        else
        {
            Datadog.Trace.AppSec.EventTrackingSdkV2.TrackUserLoginSuccess<IUserDetails?>(userLogin, null, metadata, Trace.Tracer.Instance);
        }

        return CallTargetState.GetDefault();
    }
}
