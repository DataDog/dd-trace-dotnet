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
            var url = GetUrl(uri, queryStringManager);

            if (StringUtil.IsNullOrEmpty(userInfo))
            {
                return url;
            }

            // GetUrl() never includes the user info, so the redacted form has to be inserted at the
            // start of the authority. A scheme cannot contain '/', so the first "//" always delimits
            // it. If it somehow isn't there, return the URL that already has no credentials in it
            // rather than building something malformed.
            var schemeDelimiterIndex = url.IndexOf("//", StringComparison.Ordinal);
            if (schemeDelimiterIndex < 0)
            {
                return url;
            }

            // We must never report the credentials, but the URL should still show that they were there
            return url.Insert(
                schemeDelimiterIndex + 2,
                userInfo.IndexOf(':') >= 0 ? RedactedUserNameAndPassword : RedactedUserName);
        }

        internal static string GetUrl(string scheme, string host, int? port, string pathBase, string path, string queryString, QueryStringManager? queryStringManager = null)
        {
            if (queryStringManager != null)
            {
                queryString = queryStringManager.TruncateAndObfuscate(queryString);
                return $"{scheme}://{GetNormalizedHost(host)}{(port.HasValue ? $":{port}" : string.Empty)}{pathBase}{path}{queryString}";
            }

            return $"{scheme}://{GetNormalizedHost(host)}{(port.HasValue ? $":{port}" : string.Empty)}{pathBase}{path}";
        }

        internal static string GetNormalizedHost(string? host) => StringUtil.IsNullOrEmpty(host) ? NoHostSpecified : host;
    }
}
