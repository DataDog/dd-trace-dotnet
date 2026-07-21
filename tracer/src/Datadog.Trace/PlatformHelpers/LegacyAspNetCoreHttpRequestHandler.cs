// <copyright file="LegacyAspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.PlatformHelpers
{
    internal sealed class LegacyAspNetCoreHttpRequestHandler
    {
        private const string OperationName = "aspnet_core.request";

        private readonly IDatadogLogger _log;

        public LegacyAspNetCoreHttpRequestHandler(IDatadogLogger log)
        {
            _log = log;
        }

        public Scope StartAspNetCorePipelineScope(
            Tracer tracer,
            LegacyAspNetCoreDiagnosticObserver.HttpRequestStruct request)
        {
            // See also AspNetCoreHttpRequestHandler for the .NET Core implementation
            var tags = new AspNetCoreTags();

            var host = request.Host.Value ?? string.Empty;
            var httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            var pathBase = request.PathBase.ToUriComponent();
            var requestPath = request.Path.ToUriComponent();
            var url = HttpRequestUtils.GetUrl(
                request.Scheme ?? string.Empty,
                host,
                port: null, // The request.Host includes the port
                pathBase,
                requestPath,
                request.QueryString.Value ?? string.Empty,
                tracer.TracerManager.QueryStringManager);
            var userAgent = request.Headers is { Instance: not null } headers && headers[HttpHeaderNames.UserAgent] is { } ua
                                ? string.Join(",", ua)
                                : null;
            userAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent;

            // TODO: we should only create this resource name string if we actually need it, due to head sampling etc
            var absolutePath = string.IsNullOrEmpty(pathBase) ? requestPath : pathBase + requestPath;
            var resourceUrl = UriHelpers.GetCleanUriPath(absolutePath).ToLowerInvariant();
            var resource = $"{httpMethod} {resourceUrl}";

            var extractedContext = ExtractPropagatedContext(tracer, request.Headers).MergeBaggageInto(Baggage.Current);

            var scope = tracer.StartActiveInternal(OperationName, extractedContext.SpanContext, tags: tags, links: extractedContext.Links);
            var span = scope.Span;
            span.DecorateWebServerSpan(resource, httpMethod, host, url, userAgent, tags);
#pragma warning disable CS8620 // HeaderTags values are non-null, while AddHeadersToSpanAsTags also accepts nullable values.

            var headerTagsInternal = tracer.CurrentTraceSettings.Settings.HeaderTags;
            if (headerTagsInternal.Count != 0)
            {
                try
                {
                    // extract propagation details from http headers
                    if (request.Headers is { Instance: not null } requestHeaders)
                    {
                        tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(
                            span,
                            new LegacyAspNetCoreHeadersCollectionAdapter(requestHeaders),
                            headerTagsInternal,
                            defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(span, extractedContext.Baggage, tracer.Settings.BaggageTagKeys);

            // TODO: Collect IP header (requires more duck typing)
            tags.SetAnalyticsSampleRate(LegacyAspNetCoreDiagnosticObserver.IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(LegacyAspNetCoreDiagnosticObserver.IntegrationId);
            return scope;
        }

        public void StopAspNetCorePipelineScope(Tracer tracer, Scope scope, LegacyAspNetCoreDiagnosticObserver.HttpResponseStruct response)
        {
            try
            {
                var settings = tracer.CurrentTraceSettings.Settings;
                // TODO: Update resource name if required, once we delay setting it
                if (!scope.Span.HasHttpStatusCode())
                {
                    scope.Span.SetHttpStatusCode(response.StatusCode, isServer: true, settings);
                }

                if (settings.HeaderTags.Count != 0 && response.Headers is { Instance: not null } headers)
                {
                    scope.Span.SetHeaderTags(
                        new LegacyAspNetCoreHeadersCollectionAdapter(headers),
                        settings.HeaderTags,
                        defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                }
            }
            finally
            {
                scope.Dispose();
            }
        }

        public void HandleAspNetCoreException(Tracer tracer, Scope scope, Exception exception)
        {
            var statusCode = exception.TryDuckCast<LegacyAspNetCoreDiagnosticObserver.BadHttpRequestExceptionStruct>(out var badRequestException)
                                 ? badRequestException.StatusCode
                                 : 500;

            scope.Span.SetHttpStatusCode(statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);
            scope.Span.SetException(exception);
        }

        private PropagationContext ExtractPropagatedContext<T>(Tracer tracer, T? headers)
            where T : ILegacyAspNetCoreHeaders
        {
            try
            {
                if (headers?.Instance is not null)
                {
                    return tracer.TracerManager.SpanContextPropagator.Extract(new LegacyAspNetCoreHeadersCollectionAdapter(headers));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return default;
        }
    }
}

#endif
