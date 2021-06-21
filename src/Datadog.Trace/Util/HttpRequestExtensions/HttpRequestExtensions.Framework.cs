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

namespace Datadog.Trace.Util
{
    internal static class HttpRequestExtensions
    {
        internal static Dictionary<string, object> PrepareArgsForWaf(this HttpRequest request, RouteData routeDatas = null)
        {
            var headersDic = new Dictionary<string, string>();
            foreach (var k in request.Headers.AllKeys)
            {
                headersDic.Add(k, request.Headers[k]);
            }

            var cookiesDic = new Dictionary<string, string>();
            foreach (var k in request.Cookies.AllKeys)
            {
                cookiesDic.Add(k, request.Cookies[k].Value);
            }

            var dict = new Dictionary<string, object>()
            {
                { AddressesConstants.RequestMethod, request.HttpMethod },
                { AddressesConstants.RequestUriRaw, request.Url.AbsoluteUri },
                { AddressesConstants.RequestQuery, request.Url.Query },
                { AddressesConstants.RequestHeaderNoCookies, headersDic },
                { AddressesConstants.RequestCookies, cookiesDic },
            };

            if (routeDatas != null && routeDatas.Values.Any())
            {
                dict.Add(AddressesConstants.RequestPathParams, routeDatas.Values.ToDictionary(c => c.Key, c => c.Value));
            }

            return dict;
        }
    }
}
#endif
