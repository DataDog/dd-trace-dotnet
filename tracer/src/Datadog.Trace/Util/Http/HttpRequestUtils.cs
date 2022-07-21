// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http.QueryStringObfuscation;

namespace Datadog.Trace.Util.Http
{
    internal class HttpRequestUtils
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";

        internal static string GetUrl(string scheme, string host, string pathBase, string path, string queryString, QueryStringManager queryStringManager = null)
        {
            if (queryStringManager != null)
            {
                queryString = queryString.Substring(0, Math.Min(queryString.Length, 200));
                queryString = queryStringManager.Obfuscate(queryString);
                return $"{scheme}://{(string.IsNullOrEmpty(host) ? NoHostSpecified : host)}{pathBase}{path}{queryString}";
            }

            return $"{scheme}://{(string.IsNullOrEmpty(host) ? NoHostSpecified : host)}{pathBase}{path}";
        }
    }
}
