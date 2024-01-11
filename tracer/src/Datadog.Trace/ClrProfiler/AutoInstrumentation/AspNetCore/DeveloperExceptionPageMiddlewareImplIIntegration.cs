// <copyright file="DeveloperExceptionPageMiddlewareImplIIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// DeveloperExceptionPageMiddlewareImpl integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Diagnostics",
    TypeName = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddlewareImpl",
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Diagnostics.ErrorContext" },
    MethodName = "DisplayException",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "5.0.0.0",
    MaximumVersion = "8.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.Iast)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DeveloperExceptionPageMiddlewareImplIIntegration
{
    private const string IntegrationName = nameof(IntegrationId.AspNetCore);

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="errorContext">The context of the error.</param>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object errorContext)
    {
        errorContext.TryDuckCast<ErrorContextStruct>(out var errorContextStruct);
        return new CallTargetState(IastModule.OnStackTraceLeak(errorContextStruct.Exception, IntegrationId.AspNetCore).SingleSpan);
    }

    [DuckCopy]
    internal struct ErrorContextStruct
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public Exception Exception;
    }
}

#endif
