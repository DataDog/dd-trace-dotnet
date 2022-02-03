// <copyright file="LambdaRequestBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaRequestBuilder : ILambdaExtensionRequest
    {
        private const string EndInvocationPath = "/lambda/end-invocation";
        private const string StartInvocationPath = "/lambda/start-invocation";
        private const string TraceContextPath = "/trace-context";
        private const string TraceContextUri = "http://127.0.0.1:8124";
        private const string TraceContextUriEnvName = "_DD_TRACE_CONTEXT_ENDPOINT";

        internal LambdaRequestBuilder()
        {
            Uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
        }

        internal string Uri { get; }

        WebRequest ILambdaExtensionRequest.GetTraceContextRequest()
        {
            var request = WebRequest.Create(Uri + TraceContextPath);
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            return request;
        }

        WebRequest ILambdaExtensionRequest.GetStartInvocationRequest()
        {
            var request = WebRequest.Create(Uri + StartInvocationPath);
            request.Method = "POST";
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            request.ContentType = MimeTypes.Json;
            return request;
        }

        WebRequest ILambdaExtensionRequest.GetEndInvocationRequest(bool isError)
        {
            var request = WebRequest.Create(Uri + EndInvocationPath);
            request.Method = "POST";
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            if (isError)
            {
                request.Headers.Set(HttpHeaderNames.InvocationError, "true");
            }

            return request;
        }
    }
}
