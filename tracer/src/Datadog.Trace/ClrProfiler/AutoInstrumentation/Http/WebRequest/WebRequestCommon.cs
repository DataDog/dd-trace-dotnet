// <copyright file="WebRequestCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    internal class WebRequestCommon
    {
        internal const string NetFrameworkAssembly = "System";
        internal const string NetCoreAssembly = "System.Net.Requests";

        internal const string HttpWebRequestTypeName = "System.Net.HttpWebRequest";
        internal const string WebRequestTypeName = "System.Net.WebRequest";
        internal const string WebResponseTypeName = "System.Net.WebResponse";
        internal const string WebResponseTask = "System.Threading.Tasks.Task`1<" + WebResponseTypeName + ">";

        internal const string Major2 = "2";
        internal const string Major4 = "4";
        internal const string Major6 = "6";

        internal const string IntegrationName = nameof(Configuration.IntegrationId.WebRequest);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.WebRequest;

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
                var spanContext = SpanContextPropagator.Instance.Extract(request.Headers.Wrap());

                // If this operation creates the trace, then we need to re-apply the sampling priority
                bool setSamplingPriority = spanContext?.SamplingPriority != null && Tracer.InternalInstance.ActiveScope == null;

                Scope scope = null;

                try
                {
                    scope = ScopeFactory.CreateOutboundHttpScope(Tracer.InternalInstance, request.Method, request.RequestUri, IntegrationId, out var tags, spanContext?.TraceId, spanContext?.SpanId);

                    if (scope != null)
                    {
                        if (setSamplingPriority)
                        {
                            scope.Span.SetTraceSamplingPriority(spanContext.SamplingPriority.Value);
                            scope.Span.Context.TraceContext.LockSamplingPriority();
                        }

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());

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
