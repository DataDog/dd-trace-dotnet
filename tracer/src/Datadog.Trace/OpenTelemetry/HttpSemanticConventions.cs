// <copyright file="HttpSemanticConventions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
        /// standardized methods in RFC 9110, plus PATCH and QUERY.
        /// </summary>
        internal const string OtherRequestMethod = "_OTHER";

        /// <summary>
        /// The span name used when the request method is not one of the standardized methods in
        /// RFC 9110, plus PATCH and QUERY.
        /// </summary>
        internal const string UnknownMethodSpanName = "HTTP";

        /// <summary>
        /// The value reported in "http.route" for a route template that matches the application root.
        /// </summary>
        private const string RootRoute = "/";

        // The values reported in "network.protocol.version" for the protocol versions we expect to
        // see. Held as constants so the common cases don't allocate a string per request.
        private const string ProtocolVersion10 = "1.0";
        private const string ProtocolVersion11 = "1.1";
        private const string ProtocolVersion20 = "2";
        private const string ProtocolVersion30 = "3";

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
            GetRequestMethodAttributeValues(httpMethod, out string httpRequestMethod, out string? httpRequestMethodOriginal);
            tags.HttpMethod = httpRequestMethod;
            tags.HttpRequestMethodOriginal = httpRequestMethodOriginal;

            // The span name is "{method} {target}", but there is no low-cardinality target available
            // for HTTP client spans until we support "url.template", so we only use the method.
            // Note that we must not fall back to using the URI path as the target.
            span.ResourceName = GetResourceName(httpRequestMethod);

            if (requestUri is not null)
            {
                tags.HttpUrl = HttpRequestUtils.GetUrlFull(requestUri, queryStringManager);
                tags.Host = GetServerAddress(requestUri.Host);
                tags.ServerPort = GetServerPort(requestUri);
            }
        }

        /// <summary>
        /// Sets the span name and the request tags of an HTTP server span, using the OpenTelemetry
        /// HTTP semantic conventions. This is the OpenTelemetry-semantics counterpart of
        /// <see cref="ExtensionMethods.SpanExtensions.DecorateWebServerSpan"/>: callers must use
        /// exactly one of the two for a given span, never both.
        /// </summary>
        /// <param name="span">The HTTP server span.</param>
        /// <param name="tags">The tags of <paramref name="span"/>.</param>
        /// <param name="resourceName">The resource name to assign to <paramref name="span"/>.</param>
        /// <param name="originalMethod">The HTTP method as reported by the framework, before normalization.</param>
        /// <param name="userAgent">The value of the "User-Agent" request header.</param>
        /// <param name="scheme">The request scheme, e.g. <c>https</c>.</param>
        /// <param name="host">The server host, without the port.</param>
        /// <param name="port">The server port, if known.</param>
        /// <param name="pathBase">The application path base, already URI-encoded.</param>
        /// <param name="path">The request path, already URI-encoded.</param>
        /// <param name="queryString">The raw query string, including the leading '?' if present.</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string.</param>
        internal static void SetHttpServerRequestValues(
            Span span,
            WebTags tags,
            string? resourceName,
            string? originalMethod,
            string? userAgent,
            string? scheme,
            string? host,
            int? port,
            string? pathBase,
            string? path,
            string? queryString,
            QueryStringManager? queryStringManager)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();

            tags.HttpUserAgent = userAgent;

            GetRequestMethodAttributeValues(originalMethod, out string httpRequestMethod, out string? httpRequestMethodOriginal);
            tags.HttpMethod = httpRequestMethod;
            tags.HttpRequestMethodOriginal = httpRequestMethodOriginal;

            SetHttpServerUrlTags(tags, scheme, host, port, pathBase, path, queryString, queryStringManager);
        }

        /// <summary>
        /// Gets the value to report in "server.address" for <paramref name="host"/>. Same as
        /// <see cref="HttpRequestUtils.GetNormalizedHost"/>, except that the brackets that
        /// <see cref="Uri.Host"/> and ASP.NET Core's HostString.Host put around IPv6
        /// addresses (for example, "[::1]") are stripped, as OpenTelemetry expects the address
        /// itself. The brackets are only kept in "url.full".
        /// </summary>
        internal static string? GetServerAddress(string? host)
        {
            // TODO: Follow the logic of .NET to prefer host headers as seen in https://github.com/dotnet/runtime/blob/c3901aafba2513705da036e20fdfde3058aa74e5/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHelper.cs#L42
            if (StringUtil.IsNullOrEmpty(host))
            {
                return null;
            }

            return host.Length > 1 && host[0] == '[' && host[host.Length - 1] == ']'
                       ? host.Substring(1, host.Length - 2)
                       : host;
        }

        /// <summary>
        /// Gets the value to report in "server.port" for <paramref name="requestUri"/>. This is
        /// <see cref="Uri.Port"/>, which is the default port for the scheme when the URL doesn't
        /// specify one, except when the scheme has no registered default port and none was
        /// specified either (for example, "http+unix://socket/path"). In that case,
        /// <see cref="Uri.Port"/> is -1, which is not a valid port, so we omit the tag instead.
        /// </summary>
        internal static int? GetServerPort(Uri requestUri)
            => requestUri.Port >= 0 ? requestUri.Port : null;

        /// <summary>
        /// Sets the response tags of an HTTP client span, using the OpenTelemetry HTTP semantic
        /// conventions. Does nothing when OpenTelemetry semantics are disabled.
        /// </summary>
        /// <param name="span">The HTTP client span</param>
        /// <param name="protocolVersion">The protocol version of the response, if one was received</param>
        internal static void SetHttpClientResponseValues(Span span, Version? protocolVersion)
        {
            if (span.OpenTelemetrySemanticsEnabled && span.Tags is HttpTags tags)
            {
                tags.NetworkProtocolVersion = GetNetworkProtocolVersion(protocolVersion);
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
        internal static void SetHttpServerUrlTags(
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
                tags.ServerAddress = GetServerAddress(host);

                // server.port is only set when server.address is set
                tags.ServerPort = port;
            }
        }

        /// <summary>
        /// Calculates the values to report in <c>http.request.method</c> and <c>http.request.method_original</c> given the original method.
        /// </summary>
        /// <param name="originalMethod">The request method as reported by the framework.</param>
        /// <param name="normalizedMethod">The normalized value to be written to <c>http.request.method</c>.</param>
        /// <param name="originalMethodOriginal">The original value to be written to <c>http.request.method_original</c>, or <c>null</c> if the original method (in its canonical form) is the same as the normalized one.</param>
        internal static void GetRequestMethodAttributeValues(string? originalMethod, out string normalizedMethod, out string? originalMethodOriginal)
        {
            normalizedMethod = NormalizeRequestMethod(originalMethod);
            originalMethodOriginal = normalizedMethod == OtherRequestMethod ? originalMethod : null;
        }

        /// <summary>
        /// Gets the value to report in "network.protocol.version" for the protocol version of a
        /// response, or <c>null</c> if no response was received. Note that the minor version is
        /// only included for HTTP/1.x, so HTTP/2 is reported as "2" rather than "2.0".
        /// </summary>
        internal static string? GetNetworkProtocolVersion(Version? protocolVersion)
            => protocolVersion switch
            {
                null => null,
                { Major: 1, Minor: 0 } => ProtocolVersion10,
                { Major: 1, Minor: 1 } => ProtocolVersion11,
                { Major: 2, Minor: 0 } => ProtocolVersion20,
                { Major: 3, Minor: 0 } => ProtocolVersion30,
                _ => protocolVersion.ToString(),
            };

        /// <summary>
        /// Gets the value to report in "http.route" for the route template the server matched, or
        /// <c>null</c> if no route matched. ASP.NET Core stores a template that matches the
        /// application root as the empty string, which must not be reported verbatim: it would emit
        /// an empty attribute and leave a trailing space in the "{method} {http.route}" span name.
        /// ASP.NET Core's own "http.route" and the OpenTelemetry ASP.NET Core instrumentation both
        /// report "/" in that case, so we do the same.
        /// </summary>
        /// <param name="routeTemplate">The route template as stored by the server.</param>
        internal static string? GetHttpRoute(string? routeTemplate)
            => routeTemplate is { Length: 0 } ? RootRoute : routeTemplate;

        /// <summary>
        /// Gets the resource name for a request with the provided "http.request.method" value, when no
        /// low-cardinality target is available.
        /// </summary>
        internal static string GetResourceName(string requestMethod)
            => string.Equals(requestMethod, OtherRequestMethod, StringComparison.Ordinal)
                   ? UnknownMethodSpanName
                   : requestMethod;

        /// <summary>
        /// Gets the value to report in "http.request.method": the canonical form of
        /// <paramref name="httpMethod"/>, or <see cref="OtherRequestMethod"/> if it is not one of
        /// the methods defined in
        /// <see href="https://www.rfc-editor.org/rfc/rfc9110.html#name-methods">RFC 9110</see>, plus
        /// PATCH and QUERY.
        /// </summary>
        private static string NormalizeRequestMethod(string? httpMethod)
        {
            if (StringUtil.IsNullOrEmpty(httpMethod))
            {
                return OtherRequestMethod;
            }

            // Fast path: the method is already in its canonical form, which is the common case.
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
            // so treat a case-insensitive match as the canonical method. Inlined as ordinal
            // case-insensitive comparisons rather than a dictionary lookup, since there are few
            // enough methods that this avoids the overhead of computing a case-insensitive hash.
            if (string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return "GET";
            }

            if (string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return "POST";
            }

            if (string.Equals(httpMethod, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                return "PUT";
            }

            if (string.Equals(httpMethod, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                return "DELETE";
            }

            if (string.Equals(httpMethod, "PATCH", StringComparison.OrdinalIgnoreCase))
            {
                return "PATCH";
            }

            if (string.Equals(httpMethod, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                return "CONNECT";
            }

            if (string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return "HEAD";
            }

            if (string.Equals(httpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return "OPTIONS";
            }

            if (string.Equals(httpMethod, "QUERY", StringComparison.OrdinalIgnoreCase))
            {
                return "QUERY";
            }

            if (string.Equals(httpMethod, "TRACE", StringComparison.OrdinalIgnoreCase))
            {
                return "TRACE";
            }

            return OtherRequestMethod;
        }
    }
}
