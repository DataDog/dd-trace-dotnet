// <copyright file="WebRequest_GetResponseAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for WebRequest.GetResponseAsync
    /// We're actually instrumenting HttpWebRequest, but the GetResponseAsync method is declared in WebRequest (and not overriden)
    /// So instead, we instrument WebRequest and check the actual type
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.WebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = WebRequestCommon.WebResponseTask,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.WebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = WebRequestCommon.WebResponseTask,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major7,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class WebRequest_GetResponseAsync_Integration
    {
        private const string MethodName = "GetResponseAsync";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            return WebRequestCommon.GetResponse_OnMethodBegin(instance);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (state.Scope != null)
            {
                if (returnValue is HttpWebResponse response)
                {
                    Tracer tracer = Tracer.Instance;
                    state.Scope.Span.SetHttpStatusCode((int)response.StatusCode, false, tracer.Settings);
                }

                state.Scope.DisposeWithException(exception);
            }

            return returnValue;
        }
    }
}
