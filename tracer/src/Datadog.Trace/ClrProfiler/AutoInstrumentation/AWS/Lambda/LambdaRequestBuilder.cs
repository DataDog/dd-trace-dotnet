// <copyright file="LambdaRequestBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System;
using System.Globalization;
using System.Net;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Util;
#pragma warning disable CS0618 // WebRequest, HttpWebRequest, ServicePoint, and WebClient are obsolete. Use HttpClient instead.

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

internal sealed class LambdaRequestBuilder : ILambdaExtensionRequest
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

    WebRequest ILambdaExtensionRequest.GetEndInvocationRequest(Scope scope, object state, bool isError)
    {
        var request = WebRequest.Create(Uri + EndInvocationPath);
        request.Method = "POST";
        request.Headers.Set(HttpHeaderNames.TracingEnabled, "false");

        if (state != null)
        {
            request.Headers.Set("lambda-runtime-aws-request-id", (string)state);
        }

        if (scope is { Span: var span })
        {
            // TODO: add support for 128-bit trace ids in serverless
            request.Headers.Set(HttpHeaderNames.TraceId, span.TraceId128.Lower.ToString(CultureInfo.InvariantCulture));
            request.Headers.Set(HttpHeaderNames.SpanId, span.SpanId.ToString(CultureInfo.InvariantCulture));

            if (span.Context.TraceContext is { } traceContext)
            {
                var samplingPriority = traceContext.GetOrMakeSamplingDecision(span);
                request.Headers.Set(HttpHeaderNames.SamplingPriority, SamplingPriorityValues.ToString(samplingPriority));
            }

            var errorMessage = span.GetTag("error.msg");
            if (errorMessage != null)
            {
                var encodedErrMessage = System.Text.Encoding.UTF8.GetBytes(errorMessage);
                request.Headers.Set(HttpHeaderNames.InvocationErrorMsg, Convert.ToBase64String(encodedErrMessage));
            }

            var errorType = span.GetTag("error.type");
            if (errorType != null)
            {
                var encodedErrType = System.Text.Encoding.UTF8.GetBytes(errorType);
                request.Headers.Set(HttpHeaderNames.InvocationErrorType, Convert.ToBase64String(encodedErrType));
            }

            var errorStack = span.GetTag("error.stack");
            if (errorStack != null)
            {
                var encodedErrStack = System.Text.Encoding.UTF8.GetBytes(errorStack);
                request.Headers.Set(HttpHeaderNames.InvocationErrorStack, Convert.ToBase64String(encodedErrStack));
            }
        }

        if (isError)
        {
            request.Headers.Set(HttpHeaderNames.InvocationError, "true");
        }

        return request;
    }
}

#endif
