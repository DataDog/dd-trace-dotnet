// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Util.Http
{
    internal class HttpRequestUtils
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";

        internal static string GetUrl(string scheme, string host, string pathBase, string path, Func<string> getQueryString, ImmutableTracerSettings tracerSettings = null)
        {
            if (tracerSettings != null && tracerSettings.EnableQueryStringReporting)
            {
                var queryStringObfuscator = QueryStringObfuscator.Instance(tracerSettings.ObfuscationQueryStringRegexTimeout, tracerSettings.ObfuscationQueryStringRegex);
                var queryString = queryStringObfuscator.Obfuscate(getQueryString());
                return $"{scheme}://{(string.IsNullOrEmpty(host) ? NoHostSpecified : host)}{pathBase}{path}{queryString}";
            }

            return $"{scheme}://{(string.IsNullOrEmpty(host) ? NoHostSpecified : host)}{pathBase}{path}";
        }
    }
}
