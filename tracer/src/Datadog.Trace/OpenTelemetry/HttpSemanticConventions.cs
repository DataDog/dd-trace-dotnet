// <copyright file="HttpSemanticConventions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.OpenTelemetry
{
    /// <summary>
    /// Shapes the values required by the
    /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/">OpenTelemetry HTTP
    /// semantic conventions</see> and stores them on the corresponding <c>ITags</c> properties.
    /// Only used when OpenTelemetry semantics are enabled.
    /// </summary>
    internal static class HttpSemanticConventions
    {
        /// <summary>
        /// The value reported in "http.request.method" when the request method is not one of the
        /// <see cref="CanonicalRequestMethods"/>.
        /// </summary>
        internal const string OtherRequestMethod = "_OTHER";

        /// <summary>
        /// The span name used when the request method is not one of the <see cref="CanonicalRequestMethods"/>.
        /// </summary>
        internal const string UnknownMethodSpanName = "HTTP";

        /// <summary>
        /// Maps a method name to its canonical form, ignoring case. Contains the methods defined in
        /// <see href="https://www.rfc-editor.org/rfc/rfc9110.html#name-methods">RFC 9110</see>, plus
        /// PATCH and QUERY. Any other method is reported as <see cref="OtherRequestMethod"/>.
        /// </summary>
        private static readonly Dictionary<string, string> CanonicalRequestMethods =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "CONNECT", "CONNECT" },
                { "DELETE", "DELETE" },
                { "GET", "GET" },
                { "HEAD", "HEAD" },
                { "OPTIONS", "OPTIONS" },
                { "PATCH", "PATCH" },
                { "POST", "POST" },
                { "PUT", "PUT" },
                { "QUERY", "QUERY" },
                { "TRACE", "TRACE" },
            };

        /// <summary>
        /// Sets the span name and the request tags of an HTTP client span, using the OpenTelemetry
        /// HTTP semantic conventions.
        /// </summary>
        /// <param name="span">The HTTP client span</param>
        /// <param name="tags">The tags of <paramref name="span"/></param>
        /// <param name="httpMethod">The HTTP method of the request, as provided by the instrumented library</param>
        /// <param name="requestUri">The absolute URI of the request, if known</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string</param>
        internal static void SetHttpClientRequestValues(Span span, HttpTags tags, string? httpMethod, Uri? requestUri, QueryStringManager? queryStringManager)
        {
            var requestMethod = NormalizeRequestMethod(httpMethod);

            tags.HttpMethod = requestMethod;

            // "http.request.method_original" is only set when it differs from "http.request.method",
            // which happens when the method is unknown, or was not already in its canonical form.
            // Always assigned, as some integrations call this more than once for the same span.
            tags.HttpRequestMethodOriginal =
                !StringUtil.IsNullOrEmpty(httpMethod) && !string.Equals(httpMethod, requestMethod, StringComparison.Ordinal)
                    ? httpMethod
                    : null;

            // The span name is "{method} {target}", but there is no low-cardinality target available
            // for HTTP client spans until we support "url.template", so we only use the method.
            // Note that we must not fall back to using the URI path as the target.
            span.ResourceName = GetSpanName(requestMethod);

            if (requestUri is not null)
            {
                tags.HttpUrl = HttpRequestUtils.GetUrlFull(requestUri, queryStringManager);
                tags.Host = HttpRequestUtils.GetNormalizedHost(requestUri.Host);

                // Uri.Port is the default port for the scheme when the URL doesn't specify one,
                // which is what we want to report in "server.port"
                tags.ServerPort = requestUri.Port;
            }
        }

        /// <summary>
        /// Gets the value to report in "http.request.method": the canonical form of
        /// <paramref name="httpMethod"/>, or <see cref="OtherRequestMethod"/> if it is not one of
        /// the <see cref="CanonicalRequestMethods"/>.
        /// </summary>
        internal static string NormalizeRequestMethod(string? httpMethod)
        {
            if (StringUtil.IsNullOrEmpty(httpMethod))
            {
                return OtherRequestMethod;
            }

            // Fast path: the method is already in its canonical form, which is the common case.
            // Kept in sync with CanonicalRequestMethods, but written as a switch because an ordinal
            // match is measurably cheaper than the case-insensitive hash the dictionary has to compute.
            switch (httpMethod)
            {
                case "CONNECT":
                case "DELETE":
                case "GET":
                case "HEAD":
                case "OPTIONS":
                case "PATCH":
                case "POST":
                case "PUT":
                case "QUERY":
                case "TRACE":
                    return httpMethod;
            }

            // HTTP methods are case-sensitive, but the libraries we instrument are not always,
            // so treat a case-insensitive match as the canonical method. The original value is
            // reported separately in "http.request.method_original".
            return CanonicalRequestMethods.TryGetValue(httpMethod, out var canonicalMethod)
                       ? canonicalMethod
                       : OtherRequestMethod;
        }

        /// <summary>
        /// Gets the span name for a request with the provided "http.request.method" value, when no
        /// low-cardinality target is available.
        /// </summary>
        internal static string GetSpanName(string requestMethod)
            => string.Equals(requestMethod, OtherRequestMethod, StringComparison.Ordinal)
                   ? UnknownMethodSpanName
                   : requestMethod;
    }
}
