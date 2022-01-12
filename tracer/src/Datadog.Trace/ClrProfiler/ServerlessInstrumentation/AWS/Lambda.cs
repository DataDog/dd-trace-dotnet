// <copyright file="Lambda.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;

using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    /// <summary>
    /// Lambda customer handler calltarget instrumentation
    /// </summary>
    public class Lambda
    {
        private const string SpanIdHeader = "x-datadog-span-id";
        private const string TraceContextEndpoint = "http://127.0.0.1:8124/lambda/trace-context";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Lambda));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="incommingEvent">IncommingEvent value</param>
        /// <param name="context">Context value.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object incommingEvent, object context)
        {
            Log.Debug("OnMethodBegin");
            return new CallTargetState(CreatePlaceholderScope(TraceContextEndpoint, Tracer.Instance));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Log.Debug("OnMethodEnd");
            state.Scope?.Dispose();
            return new CallTargetReturn<TReturn>(returnValue);
        }

        internal static Scope CreatePlaceholderScope(string traceContextEndpoint, Tracer tracer)
        {
            Scope scope = null;
            try
            {
                WebRequest request = WebRequest.Create(traceContextEndpoint);
                request.Credentials = CredentialCache.DefaultCredentials;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                string placeholderServiceName = "placeholder-service";
                string placeholderOperationName = "placeholder-operation";
                string traceId = response.Headers.Get(HttpHeaderNames.TraceId);
                Log.Debug("trace-id received: " + traceId);
                // need to set the exact same spanId so nested spans (auto-instrumentation or manual) will have the correct parent-id
                string spanId = response.Headers.Get(SpanIdHeader);
                Log.Debug("spanId-id received: " + spanId);
                SpanContext context = tracer.CreateSpanContext(null, null, false, Convert.ToUInt64(traceId), null);
                var span = tracer.StartSpan(placeholderOperationName, null, context, placeholderServiceName, null, false, null, Convert.ToUInt64(spanId), true);
                scope = tracer.TracerManager.ScopeManager.Activate(span, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating the placeholder scope.");
            }

            return scope;
        }
    }
}
