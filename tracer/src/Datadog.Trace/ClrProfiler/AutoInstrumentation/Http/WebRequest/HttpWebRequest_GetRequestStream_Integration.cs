// <copyright file="HttpWebRequest_GetRequestStream_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for HttpWebRequest.GetRequestStream
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major8,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HttpWebRequest_GetRequestStream_Integration
    {
        private const string MethodName = "GetRequestStream";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (instance is HttpWebRequest request && WebRequestCommon.IsTracingEnabled(request))
            {
                var tracer = Tracer.Instance;

                if (tracer.Settings.IsIntegrationEnabled(WebRequestCommon.IntegrationId))
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
                        // The expected sequence of calls is GetRequestStream -> GetResponse. Headers can't be modified after calling GetRequestStream.
                        // At the same time, we don't want to set an active scope now, because it's possible that GetResponse will never be called.
                        // Instead, we generate a spancontext and inject it in the headers. GetResponse will fetch them and create an active scope with the right id.
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
