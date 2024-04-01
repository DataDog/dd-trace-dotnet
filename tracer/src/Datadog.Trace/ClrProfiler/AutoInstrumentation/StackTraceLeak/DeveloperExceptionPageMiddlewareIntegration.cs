// <copyright file="DeveloperExceptionPageMiddlewareIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.StackTraceLeak;

/// <summary>
/// DeveloperExceptionPageMiddleware integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Diagnostics",
    TypeName = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware",
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpContext", ClrNames.Exception },
    MethodName = "DisplayException",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.StackTraceLeak),
    InstrumentationCategory = InstrumentationCategory.Iast)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DeveloperExceptionPageMiddlewareIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="context">The context of the error.</param>
    /// <param name="exception">The exception to be shown.</param>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, HttpContext context, Exception exception)
    {
        return StackTraceLeakIntegrationCommon.OnExceptionLeak(IntegrationId.StackTraceLeak, exception);
    }
}
#endif
