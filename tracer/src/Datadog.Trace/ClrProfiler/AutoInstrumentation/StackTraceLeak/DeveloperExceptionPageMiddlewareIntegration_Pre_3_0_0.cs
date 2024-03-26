// <copyright file="DeveloperExceptionPageMiddlewareIntegration_Pre_3_0_0.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.StackTraceLeak;

/// <summary>
/// DeveloperExceptionPageMiddlewareImpl integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Diagnostics",
    TypeName = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware",
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Diagnostics.ErrorContext" },
    MethodName = "DisplayException",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "3.0.0",
    MaximumVersion = "6.*.*",
    IntegrationName = nameof(IntegrationId.StackTraceLeak),
    InstrumentationCategory = InstrumentationCategory.Iast)]
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Diagnostics",
    TypeName = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareImpl",
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Diagnostics.ErrorContext" },
    MethodName = "DisplayException",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "7.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = nameof(IntegrationId.StackTraceLeak),
    InstrumentationCategory = InstrumentationCategory.Iast)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DeveloperExceptionPageMiddlewareIntegration_Pre_3_0_0
{
    internal interface IErrorContext
    {
        public Exception Exception { get; }
    }

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="errorContext">The context of the error.</param>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TContext">ErrorContext type</typeparam>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext errorContext)
        where TContext : IErrorContext
    {
        // In the current implementation ErrorContext is always non-null, as is Exception
        // so this should be safe
        var exception = errorContext.Exception;
        return StackTraceLeakIntegrationCommon.OnExceptionLeak(IntegrationId.StackTraceLeak, exception);
    }
}

#endif
