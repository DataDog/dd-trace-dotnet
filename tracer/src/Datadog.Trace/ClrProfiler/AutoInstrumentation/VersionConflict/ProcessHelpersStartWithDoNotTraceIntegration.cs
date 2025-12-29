// <copyright file="ProcessHelpersStartWithDoNotTraceIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.VersionConflict;

/// <summary>
/// System.Diagnostics.Process Datadog.Trace.Util.ProcessHelpers::StartWithDoNotTrace(System.Diagnostics.ProcessStartInfo,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace",
    TypeName = "Datadog.Trace.Util.ProcessHelpers",
    MethodName = "StartWithDoNotTrace",
    ReturnTypeName = ClrNames.Process,
    ParameterTypeNames = ["System.Diagnostics.ProcessStartInfo", ClrNames.Bool],
    MinimumVersion = "2.49.0", // We only introduced ProcessHelpers.StartWithDoNotTrace() in 2.49.0
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceVersionConflict))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ProcessHelpersStartWithDoNotTraceIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TStartInfo>(ref TStartInfo? startInfo, ref bool doNotTrace)
    {
        if (doNotTrace)
        {
            // When we run in a version conflict scenario (with a 2.x Datadog.Trace version of manual instrumentation
            // and a 3.x version of auto-instrumentation), we will incorrectly trace Process.Start() calls
            // that the 2.x lib is trying to avoid tracing by setting a [ThreadStatic] bool _doNotTrace on ProcessHelpers.
            // However, because the types aren't shared between 2.x and 3.x, and because it's the 3.x types that are
            // doing the tracing, this value is not respected. To work around that, we instrument the 2.x libraries
            // and set the 3.x version, mirroring the desired behaviour.
            ProcessHelpers.ForceDoNotTrace(true);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<System.Diagnostics.Process?> OnMethodEnd<TTarget>(System.Diagnostics.Process? returnValue, Exception? exception, in CallTargetState state)
    {
        // Always reset this back to false
        ProcessHelpers.ForceDoNotTrace(false);
        return new CallTargetReturn<System.Diagnostics.Process?>(returnValue);
    }
}
