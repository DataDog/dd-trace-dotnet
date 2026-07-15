// <copyright file="LegacyAspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
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
            LegacyAspNetCoreDiagnosticObserver.LegacyAspNetCoreHttpRequestStruct request,
            LegacyAspNetCoreHeadersCollectionAdapter headersAdapter)
        {
            var extractedContext = ExtractPropagatedContext(tracer, headersAdapter).MergeBaggageInto(Baggage.Current);
            var tags = new AspNetCoreTags();

            var method = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            var host = request.Host.Value ?? string.Empty;
            var pathBase = request.PathBase.Value ?? string.Empty;
            var requestPath = request.Path.Value ?? string.Empty;
            var path = pathBase + requestPath;
            var resourceName = method + " " + UriHelpers.GetCleanUriPath(path).ToLowerInvariant();
            var url = HttpRequestUtils.GetUrl(
                request.Scheme ?? string.Empty,
                host,
                port: null,
                pathBase,
                requestPath,
                request.QueryString.Value ?? string.Empty,
                tracer.TracerManager.QueryStringManager);
            var userAgent = GetFirstHeaderValue(headersAdapter, HttpHeaderNames.UserAgent);

            tags.SetAnalyticsSampleRate(LegacyAspNetCoreDiagnosticObserver.IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);

            Scope? scope = null;
            try
            {
                scope = tracer.StartActiveInternal(OperationName, extractedContext.SpanContext, tags: tags, links: extractedContext.Links);
                scope.Span.DecorateWebServerSpan(resourceName, method, host, url, userAgent, tags);
#pragma warning disable CS8620 // HeaderTags values are non-null, while AddHeadersToSpanAsTags also accepts nullable values.
                tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(
                    scope.Span,
                    headersAdapter,
                    tracer.CurrentTraceSettings.Settings.HeaderTags,
                    defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
#pragma warning restore CS8620
                tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(scope.Span, extractedContext.Baggage, tracer.Settings.BaggageTagKeys);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(LegacyAspNetCoreDiagnosticObserver.IntegrationId);
                return scope;
            }
            catch
            {
                scope?.Dispose();
                throw;
            }
        }

        public void StopAspNetCorePipelineScope(Tracer tracer, Scope scope, LegacyAspNetCoreDiagnosticObserver.LegacyAspNetCoreHttpResponseStruct response)
        {
            if (scope.Span.GetTag(Tags.HttpStatusCode) is null)
            {
                scope.Span.SetHttpStatusCode(response.StatusCode, isServer: true, tracer.CurrentTraceSettings.Settings);
            }
        }

        public void HandleAspNetCoreException(Tracer tracer, Scope scope, Exception exception)
        {
            var statusCode = exception.TryDuckCast<LegacyAspNetCoreDiagnosticObserver.LegacyBadHttpRequestExceptionStruct>(out var badRequestException)
                                 ? badRequestException.StatusCode
                                 : 500;

            scope.Span.SetHttpStatusCode(statusCode, isServer: true, tracer.CurrentTraceSettings.Settings);
            scope.Span.SetException(exception);
        }

        private PropagationContext ExtractPropagatedContext(Tracer tracer, LegacyAspNetCoreHeadersCollectionAdapter headers)
        {
            try
            {
                return tracer.TracerManager.SpanContextPropagator.Extract(headers);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting propagated HTTP headers.");
                return default;
            }
        }

        private string? GetFirstHeaderValue(LegacyAspNetCoreHeadersCollectionAdapter headers, string name)
        {
            foreach (var value in headers.GetValues(name))
            {
                return value;
            }

            return null;
        }
    }
}

#endif
