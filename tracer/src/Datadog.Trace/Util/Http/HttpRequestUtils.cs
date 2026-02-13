// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http.QueryStringObfuscation;

#nullable enable

namespace Datadog.Trace.Util.Http
{
    internal static class HttpRequestUtils
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";

        internal static string GetUrl(Uri uri, QueryStringManager? queryStringManager = null)
        {
            var queryString = queryStringManager?.TruncateAndObfuscate(uri.Query);
            // we can avoid an extra allocation by letting Uri format itself
            // when the TruncateAndObfuscate call didn't change the querystring or if it completely removed it
            var needToManuallyAppendQuery = false;
            UriComponents components;
            if (string.IsNullOrEmpty(queryString))
            {
                components =
                    UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path;
            }
            else
            {
                needToManuallyAppendQuery = queryString != uri.Query;
                // if the query is unchanged, we can just use the original, otherwise we need to append it later
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
