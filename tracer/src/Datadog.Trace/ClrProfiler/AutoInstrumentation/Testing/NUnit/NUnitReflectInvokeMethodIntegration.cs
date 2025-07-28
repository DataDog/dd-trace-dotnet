// <copyright file="NUnitReflectInvokeMethodIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// NUnit.Framework.Internal.Reflect.InvokeMethod() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Reflect",
    MethodName = "InvokeMethod",
    ReturnTypeName = ClrNames.Object,
    ParameterTypeNames = ["System.Reflection.MethodInfo", ClrNames.Object, "System.Object[]"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NUnitReflectInvokeMethodIntegration
{
    internal static CallTargetReturn<object> OnMethodEnd<TTarget>(object returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception != null)
        {
            // Use the inner exception if available
            exception = exception.InnerException ?? exception;
            var exceptionType = exception.GetType();
            if (exceptionType.Name is "IgnoreException" or "InconclusiveException" or "SuccessException")
            {
                // IgnoreException, InconclusiveException and SuccessException are not failures, so we don't report them
                return new CallTargetReturn<object>(returnValue);
            }

            Common.Log.Debug("Reflect.InvokeMethod threw an exception: {ExceptionMessage}, reporting it to the test span", exception.ToString());
            Test.Current?.SetErrorInfo(exception);
        }

        return new CallTargetReturn<object>(returnValue);
    }
}
