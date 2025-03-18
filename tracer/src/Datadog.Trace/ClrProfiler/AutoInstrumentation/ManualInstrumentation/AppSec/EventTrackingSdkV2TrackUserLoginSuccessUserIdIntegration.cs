// <copyright file="EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec;

/// <summary>
/// System.Void Datadog.Trace.AppSec.EventTrackingSdkV2::TrackUserLoginSuccess(string userLogin, string? userId = null, IDictionary[string, string]? metadata = null) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.AppSec.EventTrackingSdkV2",
    MethodName = "TrackUserLoginSuccess",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String, System.String]"],
    MinimumVersion = "3.13.0",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(string userLogin, string? userId, IDictionary<string, string>? metadata)
    {
        Log.Warning("Instrumentation EventTrackingSdkV2TrackUserLoginSuccessUserIdIntegration v2 is kicking off");
        System.Diagnostics.Debugger.Break();
        System.Diagnostics.Debugger.Launch();
        // EventTrackingSdkV2.TrackUserLoginSuccess(userLogin, userId is not null ? new UserDetails(userId) : null, metadata, Trace.Tracer.Instance);
        return CallTargetState.GetDefault();
    }
}
