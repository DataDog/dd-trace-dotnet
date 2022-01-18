// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;

using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaCommon
    {
        private const string TraceContextEndpointEnvName = "_DD_TRACE_CONTEXT_ENDPOINT";
        private const string TraceContextEndpoint = "http://127.0.0.1:8124/lambda/trace-context";
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LambdaCommon));

        internal static Scope CreatePlaceholderScope(Tracer tracer)
        {
            Scope scope = null;
            try
            {
                string endpointFromEnv = EnvironmentHelpers.GetEnvironmentVariable(TraceContextEndpointEnvName);
                string endpoint = endpointFromEnv != null ? endpointFromEnv : TraceContextEndpoint;
                WebRequest request = WebRequest.Create(endpoint);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string traceId = response.Headers.Get(HttpHeaderNames.TraceId);
                    // need to set the exact same spanId so nested spans (auto-instrumentation or manual) will have the correct parent-id
                    string spanId = response.Headers.Get(HttpHeaderNames.SpanId);
                    Serverless.Debug("recevied traceId = " + traceId + " and span id = " + spanId);
                    var span = tracer.StartSpan(PlaceholderOperationName, null, serviceName: PlaceholderServiceName, traceId: Convert.ToUInt64(traceId), spanId: Convert.ToUInt64(spanId));
                    scope = tracer.TracerManager.ScopeManager.Activate(span, true);
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Error creating the placeholder scope." + ex);
            }

            return scope;
        }
    }
}
