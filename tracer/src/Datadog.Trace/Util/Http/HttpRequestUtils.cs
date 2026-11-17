// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.Util.Http
{
    internal static class HttpRequestUtils
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";

        // These include the '@' delimiter so they can be inserted into a URL as-is
        private const string RedactedUserName = "REDACTED@";
        private const string RedactedUserNameAndPassword = "REDACTED:REDACTED@";

#if NET6_0_OR_GREATER
        // In .NET 6+, we could theoretically bypass a bunch of allocations by using the GetComponents() method which is heavily
        // optimized. Unfortunately, in .NET FX and < .NET 6, this approach allocates a _lot_ more. And what's more
        // .NET 6+ introduces 'DangerousDisablePathAndQueryCanonicalization' which means calling GetComponents() _Throws_, and
        // we have no way to detect it with public APIs, so we rely on duck typing to identify that, and to take the
        // more allocate-y path in that case.
        internal static string GetUrl(Uri uri, QueryStringManager? queryStringManager = null)
        {
            return uri.DuckCast<UriStruct>().IsDangerousDisablePathAndQueryCanonicalization()
                       ? GetUrlForDangerousUri(uri, queryStringManager)
                       : GetUrlViaGetComponents(uri, queryStringManager);

            // Safe to call when DangerousDisablePathAndQueryCanonicalization has been set, because it doesn't use GetComponents
            static string GetUrlForDangerousUri(Uri uri, QueryStringManager? queryStringManager = null)
            {
                var queryString = queryStringManager?.TruncateAndObfuscate(uri.Query) ?? string.Empty;

                // We know that we have to have a host (because otherwise uri.Scheme would throw), so we don't have to worry about normalizing it etc
                return uri.IsDefaultPort
                           ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}{queryString}"
                           : FormattableString.Invariant($"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}{queryString}");
            }

            // Less allocation than GetUrlForDangerousUri, but not safe to call when DangerousDisablePathAndQueryCanonicalization is set
            static string GetUrlViaGetComponents(Uri uri, QueryStringManager? queryStringManager = null)
            {
                var queryString = queryStringManager?.TruncateAndObfuscate(uri.Query);

                // We can avoid an extra allocation by letting Uri format the final result with GetComponents()
                // We can do that when:
                // 1. There's no querystring
                // 2. The QueryStringManager removed the querystring entirely
                // 3. The QueryStringManager did not change the string (nothing to redact/truncate)
                // 4. The Uri was not created with UriCreationOptions.DangerousDisablePathAndQueryCanonicalization (checked in pre-conditions)
                //
                // If the querystring _does_ change, then we have to manually append it to our initial segment
                var needToManuallyAppendQuery = false;

                UriComponents components;
                if (string.IsNullOrEmpty(queryString))
                {
                    // No querystring, or no QueryStringManager
                    components =
                        UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path;
                }
                else
                {
                    // We have a QueryStringManager, did it change the value?
                    needToManuallyAppendQuery = queryString != uri.Query;

                    // if the query is unchanged, we can just use the original
                    // otherwise we need to append the new value ourselves
                    components = needToManuallyAppendQuery
                                     ? UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path
                                     : UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path | UriComponents.Query;
                }

                // We know that we have to have a host (because otherwise uri.Scheme would throw), so we don't have to worry about normalizing etc
                var formatted = uri.GetComponents(components, UriFormat.UriEscaped);
                return needToManuallyAppendQuery
                           ? $"{formatted}{queryString}"
                           : formatted;
            }
        }

#else
        internal static string GetUrl(Uri uri, QueryStringManager? queryStringManager = null)
        {
            // We know that we have to have a host (because otherwise uri.Scheme would throw), so we don't have to worry about normalizing etc
            var queryString = queryStringManager?.TruncateAndObfuscate(uri.Query) ?? string.Empty;

            // GetComponents() in early .NET allocates a lot.
            return uri.IsDefaultPort
                       ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}{queryString}"
                       : FormattableString.Invariant($"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}{queryString}");
        }
#endif

        /// <summary>
        /// Gets the absolute URL reported in the OpenTelemetry "url.full" attribute. This is the
        /// same value as <see cref="GetUrl(Uri, QueryStringManager?)"/>, except that credentials
        /// passed in the URL are redacted rather than dropped, as required by the OpenTelemetry
        /// HTTP semantic conventions.
        /// </summary>
        /// <param name="uri">The absolute URI of the request</param>
        /// <param name="queryStringManager">Used to truncate and obfuscate the query string</param>
        internal static string GetUrlFull(Uri uri, QueryStringManager? queryStringManager = null)
        {
            var userInfo = uri.UserInfo;
            if (StringUtil.IsNullOrEmpty(userInfo))
            {
                return GetUrl(uri, queryStringManager);
            }

            var redacted = userInfo.IndexOf(':') >= 0 ? RedactedUserNameAndPassword : RedactedUserName;
            var queryString = queryStringManager?.TruncateAndObfuscate(uri.Query) ?? string.Empty;

            return uri.IsDefaultPort
                    ? $"{uri.Scheme}://{redacted}{uri.Host}{uri.AbsolutePath}{queryString}"
                    : FormattableString.Invariant($"{uri.Scheme}://{redacted}{uri.Host}:{uri.Port}{uri.AbsolutePath}{queryString}");
        }

        internal static string GetUrl(string scheme, string host, int? port, string pathBase, string path, string queryString, QueryStringManager? queryStringManager = null)
        {
            if (queryStringManager != null)
            {
                queryString = queryStringManager.TruncateAndObfuscate(queryString);
                return StringUtil.Invariant($"{scheme}://{GetNormalizedHost(host)}{(port.HasValue ? ":" : string.Empty)}{port}{pathBase}{path}{queryString}");
            }

            return StringUtil.Invariant($"{scheme}://{GetNormalizedHost(host)}{(port.HasValue ? ":" : string.Empty)}{port}{pathBase}{path}");
        }

        internal static string GetNormalizedHost(string? host) => StringUtil.IsNullOrEmpty(host) ? NoHostSpecified : host;

        /// <summary>
        /// Resolves the relative <paramref name="relativeUri"/> against <paramref name="baseUri"/>.
        /// </summary>
        /// <param name="relativeUri">The relative URI to make absolute</param>
        /// <param name="baseUri">The absolute URI that <paramref name="relativeUri"/> is resolved against, if known</param>
        /// <returns>The resolved absolute URI, or <c>null</c> if <paramref name="baseUri"/> is not a usable absolute URI.</returns>
        internal static Uri? GetAbsoluteUri(Uri relativeUri, Uri? baseUri)
            => baseUri is { IsAbsoluteUri: true } && Uri.TryCreate(baseUri, relativeUri, out var absoluteUri)
                   ? absoluteUri
                   : null;

        /// <summary>
        /// Gets the path and query string of the relative <paramref name="relativeUri"/>, escaped and normalized the same
        /// way as <see cref="Uri.AbsolutePath"/> and <see cref="Uri.Query"/> of an absolute URI. Any fragment is discarded.
        /// </summary>
        /// <param name="relativeUri">The relative URI</param>
        /// <param name="path">The path, always starting with '/'</param>
        /// <param name="queryString">The query string including the leading '?', or an empty string if there is none</param>
        /// <returns><c>false</c> if <paramref name="relativeUri"/> starts with an authority, as in <c>//user:password@host/path</c>,
        /// which can include credentials and must not be reported as a path; otherwise <c>true</c>.</returns>
        internal static bool TryGetRelativeUriPathAndQuery(Uri relativeUri, out string path, out string queryString)
        {
            // Let System.Uri resolve the reference rather than parsing it by hand: it ignores leading whitespace and treats
            // '\' as '/', so " //host", "\/host" and similar are network-path references, which change the host. "\\host"
            // and "/\host" keep the host but produce a path starting with "//", which still contains the authority
            var placeholderBase = RelativeUriBase.Instance;
            if (Uri.TryCreate(placeholderBase, relativeUri, out var resolved)
             && resolved.Host == placeholderBase.Host
             && !resolved.AbsolutePath.StartsWith("//", StringComparison.Ordinal))
            {
                path = resolved.AbsolutePath;
                queryString = resolved.Query;
                return true;
            }

            path = string.Empty;
            queryString = string.Empty;
            return false;
        }

        // Created on first use only, as relative request URIs are rare
        private static class RelativeUriBase
        {
            // ".invalid" is reserved (RFC 2606), so it can't be the host of a real network-path reference
            internal static readonly Uri Instance = new("http://relative.invalid/");
        }
    }
}
