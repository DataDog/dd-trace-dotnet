// <copyright file="SessionOptionsIdleTimeoutIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Session;

/// <summary>
/// System.Void Microsoft.AspNetCore.Builder.SessionOptions::set_IdleTimeout(System.TimeSpan) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Session",
    TypeName = "Microsoft.AspNetCore.Builder.SessionOptions",
    MethodName = "set_IdleTimeout",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.TimeSpan],
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.Iast)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SessionOptionsIdleTimeoutIntegration
{
    private const string MethodName = "set_IdleTimeout";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SessionOptionsIdleTimeoutIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref TimeSpan value)
    {
        try
        {
            IastModule.OnSessionTimeout("options.IdleTimeout", value);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to report Session Idle Timeout vulnerability");
        }

        return CallTargetState.GetDefault();
    }
}
