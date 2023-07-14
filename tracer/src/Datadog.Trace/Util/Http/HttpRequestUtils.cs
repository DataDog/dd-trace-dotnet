// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util.Http
{
    internal class HttpRequestUtils
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";

        internal static string GetUrl(Uri uri, QueryStringManager queryStringManager = null)
            => GetUrl(
                uri.Scheme,
                uri.Host,
                uri.IsDefaultPort ? null : uri.Port,
                string.Empty,
                uri.AbsolutePath,
                uri.Query,
                queryStringManager);

        internal static string GetUrl(string scheme, string host, int? port, string pathBase, string path, string queryString, QueryStringManager queryStringManager = null)
        {
            // $"{scheme}://{GetNormalizedHost(host)}{(port.HasValue ? $":{port}" : string.Empty)}{pathBase}{path}{queryString}";
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            sb.Append(scheme)
              .Append("://")
              .Append(GetNormalizedHost(host));

            if (port.HasValue)
            {
                sb.Append(':')
                  .Append(port);
            }

            sb.Append(pathBase)
              .Append(path);

            if (!string.IsNullOrEmpty(queryString) && queryStringManager != null)
            {
                sb.Append(queryStringManager.TruncateAndObfuscate(queryString));
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        internal static string GetNormalizedHost(string host) => string.IsNullOrEmpty(host) ? NoHostSpecified : host;
    }
}
