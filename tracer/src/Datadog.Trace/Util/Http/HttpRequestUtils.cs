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

        internal static string GetUrl(string scheme, string host, string pathBase, string path, bool reportQueryString, Func<string> getQueryString = null)
        {
            // HTTP 1.0 requests are not required to provide a Host to be valid
            // Since this is just for display, we can provide a string that is
            // not an actual Uri with only the fields that are specified.
            // request.GetDisplayUrl(), used above, will throw an exception
            // if request.Host is null.
            string queryString = null;
            if (reportQueryString)
            {
                var queryStringObfuscator = QueryStringObfuscator.Instance();
                queryString = queryStringObfuscator.Obfuscate(getQueryString());
            }

            return $"{scheme}://{(string.IsNullOrEmpty(host) ? NoHostSpecified : host)}{pathBase}{path}{queryString}";
        }
    }
}
