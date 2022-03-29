// <copyright file="HttpRequestExtensions.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request)
        {
            var url = GetUrl(request);
            var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
            foreach (var k in request.Headers.Keys)
            {
                var currentKey = k ?? string.Empty;
                if (!currentKey.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                {
                    currentKey = currentKey.ToLowerInvariant();
#if NETCOREAPP
                    if (!headersDic.TryAdd(currentKey, request.Headers[currentKey]))
                    {
#else
                    if (!headersDic.ContainsKey(currentKey))
                    {
                        headersDic.Add(currentKey, request.Headers[currentKey]);
                    }
                    else
                    {
#endif
                        Log.Warning("Header {key} couldn't be added as argument to the waf", currentKey);
                    }
                }
            }

            var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.Keys.Count);
            for (var i = 0; i < request.Cookies.Count; i++)
            {
                var cookie = request.Cookies.ElementAt(i);
                var currentKey = cookie.Key ?? string.Empty;
                var keyExists = cookiesDic.TryGetValue(currentKey, out var value);
                if (!keyExists)
                {
                    cookiesDic.Add(currentKey, new List<string> { cookie.Value ?? string.Empty });
                }
                else
                {
                    value.Add(cookie.Value);
                }
            }

            var queryStringDic = new Dictionary<string, List<string>>(request.Query.Count);
            foreach (var kvp in request.Query)
            {
                var value = kvp.Value;
                var currentKey = kvp.Key ?? string.Empty;
                // a query string like ?test only fills the key part, in IIS it only fills the value part, aligning behaviors here (also waf tests on values only)
                if (string.IsNullOrEmpty(value))
                {
                    value = currentKey;
                    currentKey = string.Empty;
                }

                if (!queryStringDic.TryGetValue(currentKey, out var list))
                {
                    queryStringDic.Add(currentKey, new List<string> { value });
                }
                else
                {
                    list.Add(value);
                }
            }

            var dict = new Dictionary<string, object>
            {
                {
                    AddressesConstants.RequestMethod, request.Method
                },
                {
                    AddressesConstants.RequestUriRaw, url
                },
                {
                    AddressesConstants.RequestQuery, queryStringDic
                },
                {
                    AddressesConstants.RequestHeaderNoCookies, headersDic
                },
                {
                    AddressesConstants.RequestCookies, cookiesDic
                },
            };

            return dict;
        }

        internal static string GetUrl(this HttpRequest request)
        {
            if (request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host.Value}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}";
            }

            // HTTP 1.0 requests are not required to provide a Host to be valid
            // Since this is just for display, we can provide a string that is
            // not an actual Uri with only the fields that are specified.
            // request.GetDisplayUrl(), used above, will throw an exception
            // if request.Host is null.
            return $"{request.Scheme}://{HttpRequestExtensions.NoHostSpecified}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}";
        }
    }
}
#endif
