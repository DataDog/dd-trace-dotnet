// <copyright file="AwsApiGatewaySpanFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Creates spans representing requests handled by AWS API Gateway.
/// </summary>
internal sealed class AwsApiGatewaySpanFactory : IInferredSpanFactory
{
    private const string OperationName = "aws.apigateway";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AwsApiGatewaySpanFactory>();

    public Scope? CreateSpan(Tracer tracer, InferredProxyData data, ISpanContext? parent = null)
    {
        try
        {
            var resourceUrl = data.Path is null ? string.Empty : UriHelpers.GetCleanUriPath(data.Path).ToLowerInvariant();

            var tags = new InferredProxyTags
            {
                HttpMethod = data.HttpMethod,
                InstrumentationName = data.ProxyName,
                HttpUrl = $"{data.DomainName}{data.Path}",
                HttpRoute = resourceUrl,
                Stage = data.Stage,
                InferredSpan = 1,
            };

            var scope = tracer.StartActiveInternal(operationName: OperationName, parent: parent, startTime: data.StartTime, tags: tags, serviceName: data.DomainName);
            scope.Span.ResourceName = data.HttpMethod is null ? resourceUrl : $"{data.HttpMethod} {resourceUrl}";
            scope.Span.Type = SpanTypes.Web;

            return scope;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating AWS API Gateway span");
            return null;
        }
    }
}
