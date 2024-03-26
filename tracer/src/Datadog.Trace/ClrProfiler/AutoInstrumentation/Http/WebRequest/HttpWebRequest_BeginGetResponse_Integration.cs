// <copyright file="HttpWebRequest_BeginGetResponse_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for HttpWebRequest.BeginGetResponse
    /// We only instrument .NET Framework - .NET Core uses an HttpClient
    /// internally, which is already instrumented
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HttpWebRequest_BeginGetResponse_Integration
    {
        private const string MethodName = "BeginGetResponse";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="callback">The AsyncCallback delegate</param>
        /// <param name="state">An object containing state information for this asynchronous request</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, AsyncCallback callback, object state)
        {
            if (instance is HttpWebRequest request && WebRequestCommon.IsTracingEnabled(request))
            {
                var tracer = Tracer.Instance;

                // We may have already set headers
                if (request.Headers.Get(HttpHeaderNames.TraceId) is null)
                {
                    var span = ScopeFactory.CreateInactiveOutboundHttpSpan(
                        tracer,
                        request.Method,
                        request.RequestUri,
                        WebRequestCommon.IntegrationId,
                        out _,
                        traceId: TraceId.Zero,
                        spanId: 0,
                        startTime: null,
                        addToTraceContext: false);

                    if (span?.Context != null)
                    {
                        // Add distributed tracing headers to the HTTP request.
                        // We don't want to set an active scope now, because it's possible that EndGetResponse will never be called.
                        // Instead, we generate a spancontext and inject it in the headers. EndGetResponse will fetch them and create an active scope with the right id.
                        // Additionally, add the request headers to a cache to indicate that distributed tracing headers were
                        // added by us, not the application
                        SpanContextPropagator.Instance.Inject(span.Context, request.Headers.Wrap());
                        HeadersInjectedCache.SetInjectedHeaders(request.Headers);
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
