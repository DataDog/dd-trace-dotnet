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
            var requestMethod = NormalizeRequestMethod(httpMethod);

            tags.HttpMethod = requestMethod;

            // "http.request.method_original" is only set when it differs from "http.request.method", ignoring case.
            // Always assigned, as some integrations call this method more than once for the same span.
            tags.HttpRequestMethodOriginal = GetRequestMethodOriginal(httpMethod, requestMethod);

            // The span name is "{method} {target}", but there is no low-cardinality target available
            // for HTTP client spans until we support "url.template", so we only use the method.
            // Note that we must not fall back to using the URI path as the target.
            span.ResourceName = GetResourceName(requestMethod);

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
        /// <param name="protocol">The request protocol.</param>
        /// <param name="scheme">The request scheme, e.g. <c>https</c>.</param>
        /// <param name="host">The server host, without the port.</param>
        /// <param name="port">The server port, if known.</param>
        /// <param name="pathBase">The application path base, already URI-encoded.</param>
        /// <param name="path">The request path, already URI-encoded.</param>
        /// <param name="queryString">The raw query string, including the leading '?' if present.</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string.</param>
        internal static void SetHttpServerRequestValues(
            Span span,
            WebTags? tags,
            string? resourceName,
            string? originalMethod,
            string? userAgent,
            string? protocol,
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

            if (tags is not null)
            {
                var requestMethod = NormalizeRequestMethod(originalMethod);

                tags.HttpMethod = requestMethod;
                tags.HttpUserAgent = userAgent;
                tags.HttpRequestMethodOriginal = GetRequestMethodOriginal(originalMethod, requestMethod);
                tags.NetworkProtocolVersion = GetNetworkProtocolVersion(protocol);

                SetHttpServerUrlTags(tags, scheme, host, port, pathBase, path, queryString, queryStringManager);
            }
        }

        /// <summary>
        /// Sets the request tags of an HTTP server span, using the OpenTelemetry HTTP semantic
        /// conventions. The span name is not set here: it depends on the matched route, which is
        /// generally not known yet when the server span is created.
        /// </summary>
        /// <param name="span">The HTTP server span.</param>
        /// <param name="tags">The tags of the HTTP server span</param>
        /// <param name="resourceName">The resource name to assign to <paramref name="span"/>.</param>
        /// <param name="originalMethod">The HTTP method as reported by the framework, before normalization.</param>
        /// <param name="userAgent">The value of the "User-Agent" request header.</param>
        /// <param name="protocol">The request protocol.</param>
        /// <param name="hostHeader">The value of the request's Host header, if any</param>
        /// <param name="requestUri">The absolute URI of the request, if known</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string</param>
        internal static void SetHttpServerRequestValues(
            Span span,
            WebTags? tags,
            string? resourceName,
            string? originalMethod,
            string? userAgent,
            string? protocol,
            string? hostHeader,
            Uri? requestUri,
            QueryStringManager? queryStringManager)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();

            if (tags is not null)
            {
                var requestMethod = NormalizeRequestMethod(originalMethod);

                tags.HttpMethod = requestMethod;
                tags.HttpUserAgent = userAgent;
                tags.HttpRequestMethodOriginal = GetRequestMethodOriginal(originalMethod, requestMethod);
                tags.NetworkProtocolVersion = GetNetworkProtocolVersion(protocol);

                if (requestUri is not null)
                {
                    tags.UrlScheme = requestUri.Scheme;
                    tags.UrlPath = requestUri.AbsolutePath;

                    // "url.query" excludes the leading '?'
                    var query = queryStringManager?.TruncateAndObfuscate(requestUri.Query) ?? string.Empty;
                    tags.UrlQuery = query.Length switch
                    {
                        0 => null,
                        _ when query[0] == '?' => query.Substring(1),
                        _ => query,
                    };
                }

                SetServerAddressAndPort(tags, hostHeader, requestUri);
            }
        }

        /// <summary>
        /// Gets the innermost active HTTP server span, if there is one. The OpenTelemetry HTTP semantic
        /// conventions describe a single server span per request, so an instrumentation that finds one
        /// already active must enrich it rather than create a nested one. Returns <c>null</c> when the
        /// instrumentation is the first (and therefore the only) one to handle the request, which is
        /// what happens when a framework is self-hosted rather than hosted by another instrumented one.
        /// </summary>
        /// <param name="tracer">The tracer whose active scope is inspected</param>
        internal static Span? GetActiveHttpServerSpan(Tracer tracer) => GetActiveHttpServerScope(tracer)?.Span;

        /// <summary>
        /// Gets the scope of the innermost active HTTP server span, if there is one.
        /// See <see cref="GetActiveHttpServerSpan"/>.
        /// </summary>
        /// <param name="tracer">The tracer whose active scope is inspected</param>
        internal static Scope? GetActiveHttpServerScope(Tracer tracer)
        {
            for (Scope? scope = tracer.InternalActiveScope; scope is not null; scope = scope.Parent)
            {
                if (scope.Span.Tags is WebTags)
                {
                    return scope;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets "http.route" on <paramref name="span"/>, unless it already has one. A request can be
        /// handled by more than one instrumented framework, and the first route we see is the one that
        /// matched the incoming request.
        /// </summary>
        /// <param name="span">The HTTP server span</param>
        /// <param name="route">The matched route template</param>
        internal static void SetHttpRoute(Span span, string? route)
        {
            if (!StringUtil.IsNullOrEmpty(route) && StringUtil.IsNullOrEmpty(span.Tags.GetTag(Trace.Tags.HttpRoute)))
            {
                span.Tags.SetTag(Trace.Tags.HttpRoute, route);
            }
        }

        /// <summary>
        /// Sets "server.address" and "server.port" from the request's Host header, which is what the
        /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/#setting-serveraddress-and-serverport-attributes">OpenTelemetry
        /// specification</see> requires the server to report, falling back to the request URI when the
        /// request has no Host header. "server.port" is only reported when a port is available, and
        /// never without "server.address".
        /// </summary>
        internal static void SetServerAddressAndPort(WebTags tags, string? hostHeader, Uri? requestUri)
        {
            string? address;
            int? port;

            if (StringUtil.IsNullOrEmpty(hostHeader))
            {
                address = requestUri is null ? null : GetServerAddress(requestUri.Host);
                port = requestUri is null ? null : GetServerPort(requestUri);
            }
            else
            {
                // The Host header is "host" or "host:port", where an IPv6 host is wrapped in brackets.
                // The brackets are not part of the address itself, so GetServerAddress strips them.
                var closingBracketIndex = hostHeader![0] == '[' ? hostHeader.IndexOf(']') : -1;

                // An unterminated bracket means the whole value is a (malformed) address: splitting it
                // at a colon would cut the address in half.
                var separatorIndex = hostHeader[0] == '[' && closingBracketIndex < 0
                                         ? -1
                                         : hostHeader.LastIndexOf(':');

                if (separatorIndex > closingBracketIndex)
                {
                    // Whatever follows the ':' is the port, so it is never part of the address, even
                    // when the client sent something that isn't a valid port number.
                    address = GetServerAddress(hostHeader.Substring(0, separatorIndex));
                    port = TryParsePort(hostHeader, separatorIndex + 1, out var parsedPort) ? parsedPort : null;
                }
                else
                {
                    address = GetServerAddress(hostHeader);

                    // The client didn't specify a port, so it used the default one for the scheme.
                    // Reporting it would be guesswork, so leave "server.port" unset instead.
                    port = null;
                }
            }

            tags.ServerAddress = address;

            // The specification only allows "server.port" alongside "server.address"
            tags.ServerPort = address is null ? null : port;
        }

        /// <summary>
        /// Parses the port at the end of a Host header value, without allocating a substring.
        /// </summary>
        private static bool TryParsePort(string value, int startIndex, out int port)
        {
            port = 0;

            if (startIndex >= value.Length)
            {
                return false;
            }

            for (var i = startIndex; i < value.Length; i++)
            {
                var digit = value[i] - '0';
                if (digit is < 0 or > 9)
                {
                    return false;
                }

                port = (port * 10) + digit;

                if (port > 65535)
                {
                    return false;
                }
            }

            return true;
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
        /// Calculates the value to report in <c>http.request.method_original</c> when the original method differs from the normalized one.
        /// </summary>
        /// <param name="originalMethod">The request method as reported by the framework.</param>
        /// <param name="normalizedMethod">The normalized value written to <c>http.request.method</c>.</param>
        internal static string? GetRequestMethodOriginal(string? originalMethod, string normalizedMethod)
            => normalizedMethod == OtherRequestMethod ? originalMethod : null;

        /// <summary>
        /// Gets the value to report in "network.protocol.version" for a protocol string of the form
        /// "HTTP/1.1", which is how both the ASP.NET and the ASP.NET Core request objects report the
        /// protocol a request arrived over. The attribute holds the version on its own, and the minor
        /// version is only included for HTTP/1.x, so "HTTP/2" and "HTTP/2.0" are both reported as "2".
        /// Returns <c>null</c> when the protocol is unknown or isn't HTTP, so the attribute is left
        /// unset rather than carrying something the conventions don't describe.
        /// </summary>
        internal static string? GetNetworkProtocolVersion(string? protocol)
        {
            // The cases we expect, listed so the common ones don't allocate a substring per request.
            switch (protocol)
            {
                case null or "":
                    return null;
                case "HTTP/1.1":
                    return ProtocolVersion11;
                case "HTTP/2" or "HTTP/2.0":
                    return ProtocolVersion20;
                case "HTTP/3" or "HTTP/3.0":
                    return ProtocolVersion30;
                case "HTTP/1.0":
                    return ProtocolVersion10;
            }

            const string httpPrefix = "HTTP/";

            return protocol.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase)
                       ? protocol.Substring(httpPrefix.Length)
                       : null;
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
        /// Gets the value to report in "http.request.method": the canonical form of
        /// <paramref name="httpMethod"/>, or <see cref="OtherRequestMethod"/> if it is not one of
        /// the methods defined in
        /// <see href="https://www.rfc-editor.org/rfc/rfc9110.html#name-methods">RFC 9110</see>, plus
        /// PATCH and QUERY.
        /// </summary>
        internal static string NormalizeRequestMethod(string? httpMethod)
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

        /// <summary>
        /// Gets the resource name for a request with the provided "http.request.method" value, when no
        /// low-cardinality target is available.
        /// </summary>
        internal static string GetResourceName(string requestMethod)
            => string.Equals(requestMethod, OtherRequestMethod, StringComparison.Ordinal)
                   ? UnknownMethodSpanName
                   : requestMethod;

        /// <summary>
        /// Gets the resource name for an HTTP server request: "{method} {route}", or just "{method}"
        /// when the request didn't match a route. Note that we must not fall back to using the URI
        /// path as the target, as that would make the name high-cardinality.
        /// </summary>
        /// <param name="requestMethod">The value reported in "http.request.method"</param>
        /// <param name="route">The value reported in "http.route", if the request matched a route</param>
        internal static string GetServerResourceName(string requestMethod, string? route)
        {
            var method = GetResourceName(requestMethod);
            return StringUtil.IsNullOrEmpty(route) ? method : $"{method} {route}";
        }

        /// <summary>
        /// Gets the resource name for an HTTP server request whose method has not been normalized yet.
        /// </summary>
        /// <param name="httpMethod">The HTTP method of the request, as provided by the instrumented library</param>
        internal static string GetServerResourceNameFromRawMethod(string httpMethod)
            => GetServerResourceName(NormalizeRequestMethod(httpMethod), route: null);
    }
}
