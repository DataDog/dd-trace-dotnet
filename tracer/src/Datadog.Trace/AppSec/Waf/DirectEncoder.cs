// <copyright file="DirectEncoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec.Waf
{
    internal class DirectEncoder
    {
        public static Obj Encode(IDictionary<string, object> items, List<Obj> argCache)
        {
            var map = WafNative.ObjectMap();

            if (items.TryGetValue(AddressesConstants.RequestMethod, out var methodObj) && methodObj is string method)
            {
                WafNative.ObjectMapAdd(map, AddressesConstants.RequestMethod, (ulong)AddressesConstants.RequestMethod.Length, WafNative.ObjectStringLength(method, (ulong)method.Length));
            }

            if (items.TryGetValue(AddressesConstants.RequestUriRaw, out var urlObj) && urlObj is string url)
            {
                WafNative.ObjectMapAdd(map, AddressesConstants.RequestQuery, (ulong)AddressesConstants.RequestQuery.Length, WafNative.ObjectStringLength(url, (ulong)url.Length));
            }

            if (items.TryGetValue(AddressesConstants.RequestQuery, out var queryObj) && urlObj is IQueryCollection query)
            {
                var queryMap = WafNative.ObjectMap();
                foreach (var key in query.Keys)
                {
                    var values = query[key];
                    var valuesArray = WafNative.ObjectArray();
                    WafNative.ObjectMapAdd(queryMap, key, (ulong)key.Length, valuesArray);
                    foreach (var value in values)
                    {
                        WafNative.ObjectArrayAdd(valuesArray, WafNative.ObjectStringLength(value, (ulong)value.Length));
                    }
                }

                WafNative.ObjectMapAdd(map, AddressesConstants.RequestQuery, (ulong)AddressesConstants.RequestQuery.Length, queryMap);
            }

            if (items.TryGetValue(AddressesConstants.RequestHeaderNoCookies, out var headersObj) && headersObj is IHeaderDictionary headers)
            {
                var headersMap = WafNative.ObjectMap();
                foreach (var key in headers.Keys)
                {
                    if (!key.Equals("cookie", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var values = headers[key];
                        var valuesArray = WafNative.ObjectArray();
                        WafNative.ObjectMapAdd(headersMap, key, (ulong)key.Length, valuesArray);
                        foreach (var value in values)
                        {
                            WafNative.ObjectArrayAdd(valuesArray, WafNative.ObjectStringLength(value, (ulong)value.Length));
                        }
                    }
                }

                WafNative.ObjectMapAdd(map, AddressesConstants.RequestHeaderNoCookies, (ulong)AddressesConstants.RequestHeaderNoCookies.Length, headersMap);
            }

            if (items.TryGetValue(AddressesConstants.RequestCookies, out var cookiesObj) && cookiesObj is IRequestCookieCollection cookies)
            {
                var cookiesMap = WafNative.ObjectMap();
                foreach (var key in cookies.Keys)
                {
                        var value = cookies[key];
                        WafNative.ObjectMapAdd(cookiesMap, key, (ulong)key.Length, WafNative.ObjectStringLength(value, (ulong)value.Length));
                }

                WafNative.ObjectMapAdd(map, AddressesConstants.RequestCookies, (ulong)AddressesConstants.RequestCookies.Length, cookiesMap);
            }

            return new Obj(map);
        }
    }
}
#endif
