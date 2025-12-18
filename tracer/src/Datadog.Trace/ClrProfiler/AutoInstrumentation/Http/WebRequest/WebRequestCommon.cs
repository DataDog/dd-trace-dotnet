// <copyright file="WebRequestCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    internal static class WebRequestCommon
    {
        internal const string NetFrameworkAssembly = "System";
        internal const string NetCoreAssembly = "System.Net.Requests";

        internal const string HttpWebRequestTypeName = "System.Net.HttpWebRequest";
        internal const string WebRequestTypeName = "System.Net.WebRequest";
        internal const string WebResponseTypeName = "System.Net.WebResponse";
        internal const string WebResponseTask = "System.Threading.Tasks.Task`1<" + WebResponseTypeName + ">";

        internal const string Major2 = "2";
        internal const string Major4 = "4";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.WebRequest);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.WebRequest;

        public static CallTargetState GetRequestStream_OnMethodBegin<TTarget>(TTarget instance)
        {
            TryInjectHeaders(instance);

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Try to inject distributed headers. If successfully injected, returns <c>true</c>.
        /// </summary>
        /// <param name="instance">The instance to inject into</param>
        /// <returns>Returns <c>true</c> if injection was performed, and <c>/false</c> otherwise</returns>
        public static bool TryInjectHeaders<TTarget>(TTarget instance)
        {
            if (instance is HttpWebRequest request && IsTracingEnabled(request))
            {
                var tracer = Tracer.Instance;

                if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(WebRequestCommon.IntegrationId))
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
                        var context = new PropagationContext(span.Context, Baggage.Current);
                        tracer.TracerManager.SpanContextPropagator.Inject(context, request.Headers.Wrap());
                        HeadersInjectedCache.SetInjectedHeaders(request.Headers);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState GetResponse_OnMethodBegin<TTarget>(TTarget instance)
        {
            if (instance is HttpWebRequest request && IsTracingEnabled(request))
            {
                // Check if any headers were injected by a previous call to GetRequestStream
                // Since it is possible for users to manually propagate headers (which we should
                // overwrite), check our cache which will be populated with header objects
                // that we have injected context into
                PropagationContext cachedContext = default;

                var tracer = Tracer.Instance;

                if (HeadersInjectedCache.TryGetInjectedHeaders(request.Headers))
                {
                    var headers = request.Headers.Wrap();
                    cachedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);
                }

                var cachedSpanContext = cachedContext.SpanContext;
                int? cachedSamplingPriority = null;

                // If this operation creates the trace, then we need to re-apply the sampling priority
                if (tracer.ActiveScope == null)
                {
                    cachedSamplingPriority = cachedSpanContext?.SamplingPriority;
                }

                Scope scope = null;

                try
                {
                    scope = ScopeFactory.CreateOutboundHttpScope(
                        tracer,
                        request.Method,
                        request.RequestUri,
                        IntegrationId,
                        out _,
                        cachedSpanContext?.TraceId128 ?? TraceId.Zero,
                        cachedSpanContext?.SpanId ?? 0);

                    if (scope != null)
                    {
                        if (cachedSamplingPriority is { } samplingPriority)
                        {
                            scope.Span.Context.TraceContext.SetSamplingPriority(samplingPriority);
                        }

                        // add propagation headers to the HTTP request
                        var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                        var headers = request.Headers.Wrap();
                        tracer.TracerManager.SpanContextPropagator.Inject(context, headers);

                        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                        return new CallTargetState(scope);
                    }
                }
                catch
                {
                    scope?.Dispose();
                    throw;
                }
            }

            return CallTargetState.GetDefault();
        }

        internal static bool IsTracingEnabled(System.Net.WebRequest request)
        {
            // check if tracing is disabled for this request via http header
            string value = request.Headers[HttpHeaderNames.TracingEnabled];
            return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
