// <copyright file="UrlEncode2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RestSharp;

/// <summary>
/// System.Security.Cryptography.HashAlgorithm instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RestSharp",
    TypeName = "RestSharp.Extensions.StringExtensions",
    MethodName = "UrlEncode",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = new[] { ClrNames.String, "System.Text.Encoding" },
    MinimumVersion = "104.0.0",
    MaximumVersion = "112.*.*",
    InstrumentationCategory = InstrumentationCategory.Iast,
    IntegrationName = nameof(Configuration.IntegrationId.Ssrf))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class UrlEncode2Integration
{
    private static bool errorLogged = false;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="value">String being escaped.</param>
    /// <param name="encoding">Encoding being used.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(string value, System.Text.Encoding encoding)
    {
        return new CallTargetState(null, value);
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="returnValue">Return value.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>CallTargetReturn</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, CallTargetState state)
    {
        try
        {
            if (exception is null && returnValue is string value)
            {
                if (state.State is string input)
                {
                    var newValue = IastModule.OnSsrfEscape(input, value);
                    if (newValue is not null)
                    {
                        returnValue = (TReturn)(object)newValue;
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (!errorLogged)
            {
                Log.Error(e, "Error escaping Url with encoding");
                errorLogged = true;
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
