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
using Datadog.Trace.AppSec.DataFormat;

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        internal static Node PrepareArgsForWaf(this HttpRequest request, RouteData routeDatas = null)
        {
            var headersDic = new Dictionary<string, Node>(request.Headers.Keys.Count);
            var headerKeys = request.Headers.Keys;
            foreach (string k in headerKeys)
            {
                if (!k.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                {
                    headersDic.Add(k.ToLowerInvariant(), Node.NewString(request.Headers[k]));
                }
            }

            var cookiesDic = new Dictionary<string, Node>(request.Cookies.AllKeys.Length);
            foreach (var k in request.Cookies.AllKeys)
            {
                cookiesDic.Add(k, Node.NewString(request.Cookies[k].Value));
            }

            var queryDic = new Dictionary<string, Node>(request.QueryString.AllKeys.Length);
            foreach (var k in request.QueryString.AllKeys)
            {
                var values = Node.NewString(request.QueryString[k]);
                queryDic.Add(k, values);
            }

            var dict = new Dictionary<string, Node>
            {
                { AddressesConstants.RequestMethod, Node.NewString(request.HttpMethod) },
                { AddressesConstants.RequestUriRaw, Node.NewString(request.Url.AbsoluteUri) },
                { AddressesConstants.RequestQuery, Node.NewMap(queryDic) },
                { AddressesConstants.RequestHeaderNoCookies, Node.NewMap(headersDic) },
                { AddressesConstants.RequestCookies, Node.NewMap(cookiesDic) },
            };

            if (routeDatas != null && routeDatas.Values.Any())
            {
                var routeDataDict = ConvertRouteValueDictionary(routeDatas.Values);
                dict.Add(AddressesConstants.RequestPathParams, routeDataDict);
            }

            return Node.NewMap(dict);
        }
    }
}
#endif
