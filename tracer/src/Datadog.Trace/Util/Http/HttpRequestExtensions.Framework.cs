// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Datadog.Trace.AppSec;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpRequestExtensions));

        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request, RouteData routeDatas = null)
        {
            var headersDic = new Dictionary<string, string[]>(request.Headers.Keys.Count);
            var headerKeys = request.Headers.Keys;
            foreach (string k in headerKeys)
            {
                var currentKey = k ?? string.Empty;
                if (!currentKey.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                {
                    currentKey = currentKey.ToLowerInvariant();
                    if (!headersDic.ContainsKey(currentKey))
                    {
                        headersDic.Add(currentKey, request.Headers.GetValues(currentKey));
                    }
                    else
                    {
                        Log.Warning("Header {key} couldn't be added as argument to the waf", currentKey);
                    }
                }
            }

            var cookiesDic = new Dictionary<string, List<string>>(request.Cookies.AllKeys.Length);
            for (var i = 0; i < request.Cookies.Count; i++)
            {
                var cookie = request.Cookies[i];
                var keyExists = cookiesDic.TryGetValue(cookie.Name, out var value);
                if (!keyExists)
                {
                    cookiesDic.Add(cookie.Name, new List<string> { cookie.Value ?? string.Empty });
                }
                else
                {
                    value.Add(cookie.Value);
                }
            }

            var queryDic = new Dictionary<string, string[]>(request.QueryString.AllKeys.Length);
            foreach (var k in request.QueryString.AllKeys)
            {
                var currentKey = k ?? string.Empty;
                var values = request.QueryString.GetValues(currentKey);
                if (!queryDic.ContainsKey(currentKey))
                {
                    queryDic.Add(currentKey, values);
                }
                else
                {
                    Log.Warning("Query string {key} couldn't be added as argument to the waf", currentKey);
                }
            }

            var dict = new Dictionary<string, object>
            {
                {
                    AddressesConstants.RequestMethod, request.HttpMethod
                },
                {
                    AddressesConstants.RequestUriRaw, request.Url.AbsoluteUri
                },
                {
                    AddressesConstants.RequestQuery, queryDic
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
    }
}
#endif
