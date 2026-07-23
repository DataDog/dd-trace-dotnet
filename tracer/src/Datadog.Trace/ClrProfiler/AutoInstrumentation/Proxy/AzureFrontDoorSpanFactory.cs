// <copyright file="AzureFrontDoorSpanFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Creates spans representing requests handled by Azure Frontdoor.
/// </summary>
internal sealed class AzureFrontDoorSpanFactory : IInferredSpanFactory
{
    private const string OperationName = AzureFunctionsConstants.AzureFrontDoorName;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureFrontDoorSpanFactory>();

    public Scope? CreateSpan(Tracer tracer, InferredProxyData data, ISpanContext? parent = null)
    {
        try
        {
            // Azure Front Door currently sends the path relative, without a leading slash (e.g. "api/foo").
            // Trim any leading slash defensively before prepending our own, so the route/url stay
            // single-slashed ("/api/foo") even if Front Door starts sending an absolute path later.
            var normalizedPath = data.Path?.TrimStart('/');
            var resourceUrl = normalizedPath is null ? string.Empty : UriHelpers.GetCleanUriPath($"/{normalizedPath}").ToLowerInvariant();

            var tags = new InferredProxyTags
            {
                HttpMethod = data.HttpMethod,
                InstrumentationName = data.ProxyName,
                HttpUrl = $"{data.DomainName}/{normalizedPath}",
                HttpRoute = resourceUrl,
                InferredSpan = 1,
                Region = data.Region,
                Stage = data.Stage,
            };

            var scope = tracer.StartActiveInternal(operationName: OperationName, parent: parent, startTime: data.StartTime, tags: tags, serviceName: data.DomainName, serviceNameSource: "azure-frontdoor");
            scope.Span.ResourceName = data.HttpMethod is null ? resourceUrl : $"{data.HttpMethod} {resourceUrl}";
            scope.Span.Type = SpanTypes.Web;

            return scope;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Frontdoor span");
            return null;
        }
    }
}
