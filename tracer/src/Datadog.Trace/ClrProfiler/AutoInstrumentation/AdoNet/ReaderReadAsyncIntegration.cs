// <copyright file="ReaderReadAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// [*]DataReader [Command].ExecuteReader()
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ReaderReadAsyncIntegration
    {
        private static bool errorLogged = false;

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            try
            {
                if (exception is null)
                {
                    IastModule.RegisterDbRecord(instance!);
                }
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    Log.Error(e, "Error registering new db record to IAST module");
                    errorLogged = true;
                }
            }

            return returnValue;
        }
    }
}
