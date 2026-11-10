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
/// Creates spans representing requests handled by Azure Front Door.
/// </summary>
internal sealed class AzureFrontDoorSpanFactory : IInferredSpanFactory
{
    private const string OperationName = AzureFunctionsConstants.AzureFrontDoorName;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureFrontDoorSpanFactory>();

    public Scope? CreateSpan(Tracer tracer, InferredProxyData data, ISpanContext? parent = null)
    {
        try
        {
            // Front Door's {url_path} is "the part of the request URI without the arguments or
            // leading slash", so "/api/foo" arrives as "api/foo" and a request to the site root
            // arrives empty - which ParseUtility surfaces as null. Treat that as the root path
            // rather than as "no path", so the route and resource name are "/" instead of empty.
            // Any leading slash is trimmed defensively before we prepend our own, so the route and
            // url stay single-slashed even if Front Door starts sending an absolute path later.
            // https://learn.microsoft.com/en-us/azure/frontdoor/rule-set-server-variables
            var normalizedPath = data.Path?.TrimStart('/') ?? string.Empty;
            var resourceUrl = UriHelpers.GetCleanUriPath(StringUtil.ToLowerInvariant($"/{normalizedPath}"));

            // http.url is parsed as a URL downstream, so it needs a scheme. Front Door terminates
            // TLS at the edge, and the other tracers all emit "https://{domain}{path}" - the
            // cross-SDK system-tests validator asserts that form. Fall back to a path-only value
            // when the domain header is absent, rather than emitting "https:///path".
            // The APIM and AWS factories still omit the scheme; changing those alters spans for
            // shipped integrations, so it is deliberately left to a separate change.
            var httpUrl = StringUtil.IsNullOrEmpty(data.DomainName)
                              ? $"/{normalizedPath}"
                              : $"https://{data.DomainName}/{normalizedPath}";

            var tags = new InferredProxyTags
            {
                HttpMethod = data.HttpMethod,
                InstrumentationName = data.ProxyName,
                HttpUrl = httpUrl,
                HttpRoute = resourceUrl,
                InferredSpan = 1,
                Region = data.Region,
                Stage = data.Stage,
            };

            var scope = tracer.StartActiveInternal(operationName: OperationName, parent: parent, startTime: data.StartTime, tags: tags, serviceName: data.DomainName, serviceNameSource: InferredProxySpanHelper.AzureFrontDoorHeaderValue);
            scope.Span.ResourceName = data.HttpMethod is null ? resourceUrl : $"{data.HttpMethod} {resourceUrl}";
            scope.Span.Type = SpanTypes.Web;

            return scope;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Front Door span");
            return null;
        }
    }
}
