// <copyright file="AwsApiGatewaySpanFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Creates spans representing requests handled by AWS API Gateway.
/// </summary>
internal class AwsApiGatewaySpanFactory : IInferredSpanFactory
{
    private const string OperationName = "aws.api-gateway";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AwsApiGatewaySpanFactory>();

    public Scope? CreateSpan(Tracer tracer, InferredProxyData data, ISpanContext? parent = null)
    {
        try
        {
            // TODO RFC didn't specify obfuscation or quantization but I think we should
            var resourceName = $"{data.HttpMethod} {data.Path}";
            var httpUrl = $"{data.DomainName}{data.Path}";

            var tags = new InferredProxyTags
            {
                HttpMethod = data.HttpMethod,
                InstrumentationName = data.ProxyName, // TODO: check to make sure this is really what we want for the component tag
                HttpUrl = httpUrl,
                HttpRoute = data.Path,
                Stage = data.Stage
            };

            var scope = tracer.StartActiveInternal(operationName: OperationName, parent: parent, startTime: data.StartTime, tags: tags);

            scope.Span.ResourceName = resourceName;
            scope.Span.Type = SpanTypes.Web;

            // TODO RFC said to copy over all Errors - do we do this here or outside of this function as we do with the current spans
            return scope;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating AWS API Gateway span");
            return null;
        }
    }
}
