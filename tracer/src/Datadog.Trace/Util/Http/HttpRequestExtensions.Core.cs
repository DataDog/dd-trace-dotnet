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
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private const string NoHostSpecified = "UNKNOWN_HOST";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request, RouteData routeDatas = null)
        {
            var url = GetUrl(request);
            var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
            foreach (var k in request.Headers.Keys)
            {
                if (!k.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                {
                    var key = k.ToLowerInvariant();
#if NETCOREAPP
                    if (!headersDic.TryAdd(key, request.Headers[key]))
                    {
#else
                    if (!headersDic.ContainsKey(key))
                    {
                        headersDic.Add(key, request.Headers[key]);
                    }
                    else
                    {
#endif
                        Log.Warning("Header {key} couldn't be added as argument to the waf", key);
                    }
                }
            }

            var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.Keys.Count);
            for (var i = 0; i < request.Cookies.Count; i++)
            {
                var cookie = request.Cookies.ElementAt(i);
                var keyExists = cookiesDic.TryGetValue(cookie.Key, out var value);
                if (!keyExists)
                {
                    cookiesDic.Add(cookie.Key, new List<string> { cookie.Value ?? string.Empty });
                }
                else
                {
                    value.Add(cookie.Value);
                }
            }

            var queryStringDic = new Dictionary<string, string[]>(request.Query.Count);
            foreach (var kvp in request.Query)
            {
#if NETCOREAPP
                if (!queryStringDic.TryAdd(kvp.Key, kvp.Value))
                {
#else
                if (!queryStringDic.ContainsKey(kvp.Key))
                {
                    queryStringDic.Add(kvp.Key, kvp.Value);
                }
                else
                {
#endif
                    Log.Warning("Query string with {key} couldn't be added as argument to the waf", kvp.Key);
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

            if (routeDatas != null && routeDatas.Values.Any())
            {
                var routeDataDict = HttpRequestUtils.ConvertRouteValueDictionary(routeDatas.Values);
                dict.Add(AddressesConstants.RequestPathParams, routeDataDict);
            }

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
