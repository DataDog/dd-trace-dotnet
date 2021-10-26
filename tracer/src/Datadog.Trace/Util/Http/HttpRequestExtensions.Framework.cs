// <copyright file="HttpRequestExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Datadog.Trace.AppSec;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request, RouteData routeDatas = null)
        {
            var headersDic = new Dictionary<string, string>(request.Headers.Keys.Count);
            var headerKeys = request.Headers.Keys;
            foreach (string k in headerKeys)
            {
                if (!k.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                {
                    headersDic.Add(k.ToLowerInvariant(), request.Headers[k]);
                }
            }

            var cookiesDic = new Dictionary<string, string>(request.Cookies.AllKeys.Length);
            foreach (var k in request.Cookies.AllKeys)
            {
                cookiesDic.Add(k, request.Cookies[k].Value);
            }

            var queryDic = new Dictionary<string, string>(request.QueryString.AllKeys.Length);
            foreach (var k in request.QueryString.AllKeys)
            {
                var values = request.QueryString[k];
                queryDic.Add(k, values);
            }

            var dict = new Dictionary<string, object>
            {
                { AddressesConstants.RequestMethod, request.HttpMethod },
                { AddressesConstants.RequestUriRaw, request.Url.AbsoluteUri },
                { AddressesConstants.RequestQuery, queryDic },
                { AddressesConstants.RequestHeadersNoCookies, headersDic },
                { AddressesConstants.RequestCookies, cookiesDic },
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
