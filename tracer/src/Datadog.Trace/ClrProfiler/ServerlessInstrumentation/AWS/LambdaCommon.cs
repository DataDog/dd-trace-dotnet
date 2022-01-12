// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;

using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string SpanIdHeader = "x-datadog-span-id";
        private const string TraceContextEndpoint = "http://127.0.0.1:8124/lambda/trace-context";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LambdaCommon));

        internal static Scope CreatePlaceholderScope(Tracer tracer)
        {
            Scope scope = null;
            try
            {
                WebRequest request = WebRequest.Create(TraceContextEndpoint);
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
                Console.WriteLine("Error creating the placeholder scope." + ex);
            }

            return scope;
        }
    }
}
