// <copyright file="HttpResponseIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using System.ComponentModel;
using System.Web;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.StackTraceLeak;

/// <summary>
/// HttpResponseIntegration integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "System.Web",
    TypeName = "System.Web.HttpResponse",
    ParameterTypeNames = new[] { ClrNames.Exception, ClrNames.Bool },
    MethodName = "WriteErrorMessage",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.StackTraceLeak),
    InstrumentationCategory = InstrumentationCategory.Iast)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HttpResponseIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">The exception to be shown.</param>
    /// <param name="dontShowSensitiveErrors">The dontShowSensitiveErrors parameter of WriteErrorMessage.</param>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, Exception exception, bool dontShowSensitiveErrors)
    {
        if (HttpRuntime.UsingIntegratedPipeline && !dontShowSensitiveErrors)
        {
            return StackTraceLeakIntegrationCommon.OnExceptionLeak(IntegrationId.StackTraceLeak, exception);
        }

        return CallTargetState.GetDefault();
    }
}

#endif
