// <copyright file="HttpOtelHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http
{
    /// <summary>
    /// Shared helpers for shaping HTTP span values according to the
    /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/">OpenTelemetry HTTP semantic conventions</see>.
    /// These are only invoked when <c>DD_TRACE_OTEL_SEMANTICS_ENABLED=true</c>.
    /// </summary>
    /// <remarks>
    /// Wire-name selection (Datadog key vs OpenTelemetry key) is handled by the tags source generator via
    /// <c>[Tag(ddName, OtelName = otelName)]</c>, so these helpers write to the strongly-typed properties
    /// and only deal with the values whose <em>shape</em> differs between the two conventions.
    /// </remarks>
    internal static class HttpOtelHelper
    {
        /// <summary>
        /// Sentinel used for request methods that are not known to the instrumentation.
        /// </summary>
        internal const string OtherMethod = "_OTHER";

        /// <summary>
        /// The <c>{method}</c> placeholder used in the span name when the method is not known.
        /// </summary>
        internal const string UnknownMethodSpanName = "HTTP";

        // The methods "known" to the instrumentation: the RFC 9110 verbs, PATCH (RFC 5789) and QUERY.
        // https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-request-method
        private static readonly HashSet<string> KnownMethods = new(StringComparer.Ordinal)
        {
            "CONNECT",
            "DELETE",
            "GET",
            "HEAD",
            "OPTIONS",
            "PATCH",
            "POST",
            "PUT",
            "QUERY",
            "TRACE",
        };

        /// <summary>
        /// Normalizes a raw request method into a value suitable for the <c>http.request.method</c> attribute.
        /// Known methods are returned in their canonical (upper-case) form; anything else becomes <c>_OTHER</c>.
        /// </summary>
        /// <param name="rawMethod">The request method as reported by the framework.</param>
        /// <returns>The canonical method name, or <c>_OTHER</c>.</returns>
        internal static string GetRequestMethod(string? rawMethod)
        {
            if (StringUtil.IsNullOrEmpty(rawMethod))
            {
                return OtherMethod;
            }

            // The common case: the framework already gave us a canonical method, so avoid allocating.
            if (KnownMethods.Contains(rawMethod))
            {
                return rawMethod;
            }

            var upper = rawMethod.ToUpperInvariant();
            return KnownMethods.Contains(upper) ? upper : OtherMethod;
        }

        /// <summary>
        /// Gets the <c>{method}</c> placeholder to use in the span name: the normalized method, or
        /// <c>HTTP</c> when the method is not known.
        /// </summary>
        /// <param name="requestMethod">A method already normalized by <see cref="GetRequestMethod"/>.</param>
        /// <returns>The span-name method.</returns>
        internal static string GetSpanNameMethod(string? requestMethod)
            => StringUtil.IsNullOrEmpty(requestMethod) || string.Equals(requestMethod, OtherMethod, StringComparison.Ordinal)
                   ? UnknownMethodSpanName
                   : requestMethod;

        /// <summary>
        /// Records <c>http.request.method_original</c> when the raw method differs from the normalized one.
        /// </summary>
        /// <param name="tags">The span tags.</param>
        /// <param name="rawMethod">The request method as reported by the framework.</param>
        /// <param name="requestMethod">The value written to <c>http.request.method</c>.</param>
        internal static void SetRequestMethodOriginal(WebTags tags, string? rawMethod, string requestMethod)
        {
            if (!StringUtil.IsNullOrEmpty(rawMethod) && !string.Equals(rawMethod, requestMethod, StringComparison.Ordinal))
            {
                tags.HttpRequestMethodOriginal = rawMethod;
            }
        }

        /// <summary>
        /// Sets the OpenTelemetry URL and server attributes for an HTTP server span from the individual
        /// request components, so no URL parsing is required.
        /// </summary>
        /// <param name="tags">The span tags.</param>
        /// <param name="scheme">The request scheme, e.g. <c>https</c>.</param>
        /// <param name="host">The server host, without the port.</param>
        /// <param name="port">The server port, if known.</param>
        /// <param name="pathBase">The application path base, already URI-encoded.</param>
        /// <param name="path">The request path, already URI-encoded.</param>
        /// <param name="queryString">The raw query string, including the leading '?' if present.</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string.</param>
        internal static void SetServerUrlTags(
            WebTags tags,
            string? scheme,
            string? host,
            int? port,
            string? pathBase,
            string? path,
            string? queryString,
            QueryStringManager? queryStringManager)
        {
            tags.UrlScheme = scheme;
            tags.UrlPath = StringUtil.IsNullOrEmpty(pathBase) ? path : pathBase + path;

            var query = queryStringManager?.TruncateAndObfuscate(queryString) ?? queryString;
            if (!StringUtil.IsNullOrEmpty(query))
            {
                // url.query excludes the leading '?'
                tags.UrlQuery = query[0] == '?' ? query.Substring(1) : query;
            }

            if (!StringUtil.IsNullOrEmpty(host))
            {
                tags.ServerAddress = host;

                // server.port is only set when server.address is set
                tags.ServerPort = port;
            }
        }
    }
}
