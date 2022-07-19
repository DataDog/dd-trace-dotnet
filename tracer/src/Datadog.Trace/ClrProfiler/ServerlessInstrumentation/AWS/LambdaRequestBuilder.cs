// <copyright file="LambdaRequestBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Util;
#pragma warning disable CS0618 // WebRequest, HttpWebRequest, ServicePoint, and WebClient are obsolete. Use HttpClient instead.

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaRequestBuilder : ILambdaExtensionRequest
    {
        private const string EndInvocationPath = "/lambda/end-invocation";
        private const string StartInvocationPath = "/lambda/start-invocation";
        private const string ExtensionUri = "http://127.0.0.1:8124";
        private const string ExtensionUriEnvName = "_DD_EXTENSION_ENDPOINT";

        internal LambdaRequestBuilder()
        {
            Uri = EnvironmentHelpers.GetEnvironmentVariable(ExtensionUriEnvName) ?? ExtensionUri;
        }

        internal string Uri { get; }

        WebRequest ILambdaExtensionRequest.GetStartInvocationRequest()
        {
            var request = WebRequest.Create(Uri + StartInvocationPath);
            request.Method = "POST";
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            request.ContentType = MimeTypes.Json;
            return request;
        }

        WebRequest ILambdaExtensionRequest.GetEndInvocationRequest(Scope scope, bool isError)
        {
            var request = WebRequest.Create(Uri + EndInvocationPath);
            request.Method = "POST";
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            if (scope != null)
            {
                request.Headers.Set(HttpHeaderNames.TraceId, scope.Span.TraceId.ToString());
                request.Headers.Set(HttpHeaderNames.SpanId, scope.Span.SpanId.ToString());

                if (scope.Span.Context.TraceContext?.SamplingDecision is { } samplingDecision)
                {
                    request.Headers.Set(HttpHeaderNames.SamplingPriority, samplingDecision.Priority.ToString());
                }
            }

            if (isError)
            {
                request.Headers.Set(HttpHeaderNames.InvocationError, "true");
            }

            return request;
        }
    }
}
