// <copyright file="LegacyAspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Text;
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

        // Mirrors PathString.ToUriComponent() without adding an ASP.NET Core reference to the net461 tracer.
        private static string ToUriComponent(string path)
        {
            StringBuilder? builder = null;
            var segmentStart = 0;
            var index = 0;

            while (index < path.Length)
            {
                if (IsValidPathCharacter(path[index]))
                {
                    index++;
                    continue;
                }

                if (IsPercentEncodedCharacter(path, index))
                {
                    index += 3;
                    continue;
                }

                builder ??= new StringBuilder(path.Length * 3);
                builder.Append(path, segmentStart, index - segmentStart);

                var escapeStart = index++;
                while (index < path.Length
                    && !IsValidPathCharacter(path[index])
                    && !IsPercentEncodedCharacter(path, index))
                {
                    index++;
                }

                builder.Append(Uri.EscapeDataString(path.Substring(escapeStart, index - escapeStart)));
                segmentStart = index;
            }

            if (builder is null)
            {
                return path;
            }

            builder.Append(path, segmentStart, path.Length - segmentStart);
            return builder.ToString();
        }

        private static bool IsValidPathCharacter(char value)
            => value is >= 'a' and <= 'z'
            || value is >= 'A' and <= 'Z'
            || value is >= '0' and <= '9'
            || value is '-' or '.' or '_' or '~'
            || value is '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '='
            || value is ':' or '@' or '/';

        private static bool IsPercentEncodedCharacter(string value, int index)
            => index + 2 < value.Length
            && value[index] == '%'
            && IsHexadecimalCharacter(value[index + 1])
            && IsHexadecimalCharacter(value[index + 2]);

        private static bool IsHexadecimalCharacter(char value)
            => value is >= '0' and <= '9'
            || value is >= 'A' and <= 'F'
            || value is >= 'a' and <= 'f';

        // Unlike .NET Core 3+, .NET Framework always allocates for non-empty strings, even when the casing is unchanged.
        // Scan the common ASCII case first, and use the runtime implementation for changes and non-ASCII casing rules.
        internal static string ToUpperInvariantIfNeeded(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character > 0x7f || (uint)(character - 'a') <= (uint)('z' - 'a'))
                {
                    return value.ToUpperInvariant();
                }
            }

            return value;
        }

        internal static string ToLowerInvariantIfNeeded(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character > 0x7f || (uint)(character - 'A') <= (uint)('Z' - 'A'))
                {
                    return value.ToLowerInvariant();
                }
            }

            return value;
        }

        public Scope StartAspNetCorePipelineScope(
            Tracer tracer,
            LegacyAspNetCoreDiagnosticObserver.LegacyAspNetCoreHttpRequestStruct request,
            LegacyAspNetCoreHeadersCollectionAdapter headersAdapter)
        {
            var extractedContext = ExtractPropagatedContext(tracer, headersAdapter).MergeBaggageInto(Baggage.Current);
            var tags = new AspNetCoreTags();

            var method = request.Method is { } requestMethod ? ToUpperInvariantIfNeeded(requestMethod) : "UNKNOWN";
            var host = request.Host.Value ?? string.Empty;
            var pathBase = request.PathBase.Value ?? string.Empty;
            var requestPath = request.Path.Value ?? string.Empty;
            var escapedPathBase = ToUriComponent(pathBase);
            var escapedRequestPath = ToUriComponent(requestPath);
            var cleanPath = UriHelpers.GetCleanUriPath(escapedPathBase + escapedRequestPath);
            var resourceName = method + " " + ToLowerInvariantIfNeeded(cleanPath);
            var url = HttpRequestUtils.GetUrl(
                request.Scheme ?? string.Empty,
                host,
                port: null,
                escapedPathBase,
                escapedRequestPath,
                request.QueryString.Value ?? string.Empty,
                tracer.TracerManager.QueryStringManager);
            var userAgent = GetHeaderValue(headersAdapter, HttpHeaderNames.UserAgent);

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
            var settings = tracer.CurrentTraceSettings.Settings;
            if (!scope.Span.HasHttpStatusCode())
            {
                scope.Span.SetHttpStatusCode(response.StatusCode, isServer: true, settings);
            }

            if (!settings.HeaderTags.IsNullOrEmpty()
             && response.Headers is not null
             && response.Headers.TryDuckCast<ILegacyAspNetCoreHeaders>(out var headers))
            {
                scope.Span.SetHeaderTags(
                    new LegacyAspNetCoreHeadersCollectionAdapter(headers),
                    settings.HeaderTags,
                    defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
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

        private string? GetHeaderValue(LegacyAspNetCoreHeadersCollectionAdapter headers, string name)
        {
            using var enumerator = headers.GetValues(name).GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return null;
            }

            var first = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                return first;
            }

            var builder = StringBuilderCache.Acquire();
            builder.Append(first);
            do
            {
                builder.Append(',');
                builder.Append(enumerator.Current);
            }
            while (enumerator.MoveNext());

            return StringBuilderCache.GetStringAndRelease(builder);
        }
    }
}

#endif
