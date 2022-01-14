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
        private const string TraceContextEndpoint = "http://127.0.0.1:8124/lambda/trace-context";
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LambdaCommon));

        internal static Scope CreatePlaceholderScope(Tracer tracer)
        {
            Scope scope = null;
            try
            {
                WebRequest request = WebRequest.Create(TraceContextEndpoint);
                request.Credentials = CredentialCache.DefaultCredentials;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string traceId = response.Headers.Get(HttpHeaderNames.TraceId);
                    // need to set the exact same spanId so nested spans (auto-instrumentation or manual) will have the correct parent-id
                    string spanId = response.Headers.Get(HttpHeaderNames.SpanId);
                    Log.Debug("traceId received: {traceId}, spanId received: {spanId}", traceId, spanId);
                    var span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), spanId: Convert.ToUInt64(spanId));
                    scope = tracer.TracerManager.ScopeManager.Activate(span, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error creating the placeholder scope." + ex);
            }

            return scope;
        }
    }
}
