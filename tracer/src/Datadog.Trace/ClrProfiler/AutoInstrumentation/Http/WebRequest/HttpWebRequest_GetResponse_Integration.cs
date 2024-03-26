// <copyright file="HttpWebRequest_GetResponse_Integration.cs" company="Datadog">
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
    /// CallTarget integration for HttpWebRequest.GetResponse
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = WebRequestCommon.WebResponseTypeName,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = WebRequestCommon.WebResponseTypeName,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major8,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HttpWebRequest_GetResponse_Integration
    {
        private const string MethodName = "GetResponse";

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
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (state.Scope != null)
            {
                if (returnValue is HttpWebResponse response)
                {
                    state.Scope.Span.SetHttpStatusCode((int)response.StatusCode, false, Tracer.Instance.Settings);
                    state.Scope.Dispose();
                }
                else if (exception is WebException { Status: WebExceptionStatus.ProtocolError, Response: HttpWebResponse exceptionResponse })
                {
                    // Add the exception tags without setting the Error property
                    // SetHttpStatusCode will mark the span with an error if the StatusCode is within the configured range
                    state.Scope.Span.SetExceptionTags(exception);

                    state.Scope.Span.SetHttpStatusCode((int)exceptionResponse.StatusCode, false, Tracer.Instance.Settings);
                    state.Scope.Dispose();
                }
                else
                {
                    state.Scope.DisposeWithException(exception);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
