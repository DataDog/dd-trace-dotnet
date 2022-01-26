// <copyright file="LambdaRequestBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal class LambdaRequestBuilder : ILambdaRequest
    {
        private const string EndInvocationPath = "/lambda/end-invocation";
        private const string StartInvocationPath = "/lambda/start-invocation";
        private const string TraceContextPath = "/trace-context";
        private const string TraceContextUri = "http://127.0.0.1:8124";
        private const string TraceContextUriEnvName = "_DD_TRACE_CONTEXT_ENDPOINT";
        private const string PlaceholderServiceName = "placeholder-service";
        private const string PlaceholderOperationName = "placeholder-operation";

        WebRequest ILambdaRequest.GetTraceContextRequest()
        {
            var uri = EnvironmentHelpers.GetEnvironmentVariable(TraceContextUriEnvName) ?? TraceContextUri;
            var request = WebRequest.Create(uri + TraceContextPath);
            request.Credentials = CredentialCache.DefaultCredentials;
            request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");
            return request;
        }
    }
}
