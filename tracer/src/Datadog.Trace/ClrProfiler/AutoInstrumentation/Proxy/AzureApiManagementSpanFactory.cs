// <copyright file="AzureApiManagementSpanFactory.cs" company="Datadog">
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
/// Creates spans representing requests handled by Azure API Management.
/// </summary>
internal sealed class AzureApiManagementSpanFactory : IInferredSpanFactory
{
    private const string OperationName = "azure.apim";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureApiManagementSpanFactory>();

    public Scope? CreateSpan(Tracer tracer, InferredProxyData data, ISpanContext? parent = null)
    {
        try
        {
            if (data.Path is null)
            {
                return null;
            }

            var resourceUrl = UriHelpers.GetCleanUriPath(data.Path).ToLowerInvariant();

            if (data.DomainName is null)
            {
                Log.Debug("DomainName is Null");
            }

            var tags = new InferredProxyTags
            {
                HttpMethod = data.HttpMethod,
                InstrumentationName = data.ProxyName,
                HttpUrl = $"{data.DomainName}{data.Path}",
                HttpRoute = resourceUrl,
                InferredSpan = 1,
            };

            var scope = tracer.StartActiveInternal(operationName: OperationName, parent: parent, startTime: data.StartTime, tags: tags, serviceName: data.DomainName);
            scope.Span.ResourceName = data.HttpMethod is null ? resourceUrl : $"{data.HttpMethod.ToUpperInvariant()} {resourceUrl}";
            scope.Span.Type = SpanTypes.Web;

            return scope;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure API Management span");
            return null;
        }
    }
}
