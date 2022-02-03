// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Web.Routing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies;
using Datadog.Trace.DuckTyping;
#endif
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.Util.Http
{
    internal static class HttpRequestUtils
    {
#if NETFRAMEWORK
        private static Type _cachedListOfRouteDataType;
#endif

        internal static object ConvertRouteValueDictionary(RouteValueDictionary routeDataDict)
        {
            return routeDataDict.ToDictionary(
                c => c.Key,
                c =>
                    c.Value switch
                    {
                        List<RouteData> routeDataList => ConvertRouteValueList(routeDataList),
                        _ => c.Value?.ToString()
                    });
        }

        private static object ConvertRouteValueList(List<RouteData> routeDataList)
        {
            return routeDataList.Select(x => ConvertRouteValueDictionary(x.Values)).ToList();
        }

#if NETFRAMEWORK
        internal static object ConvertRouteValueDictionary(IRouteData routeData)
        {
            if (_cachedListOfRouteDataType == null)
            {
                var routeDataType = routeData.Instance.GetType();
                var genericListType = typeof(List<>);
                _cachedListOfRouteDataType = genericListType.MakeGenericType(new Type[] { routeDataType });
            }

            var returnDict = new Dictionary<string, object>(routeData.Values.Count);
            foreach (var kvp in routeData.Values)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                // Do a quick runtime check to determine if the value is List<RouteData>
                if (_cachedListOfRouteDataType.IsAssignableFrom(value.GetType())
                    && value is IList list)
                {
                    List<object> returnList = new List<object>(list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].TryDuckCast<IRouteData>(out var innerRouteData))
                        {
                            returnList.Add(ConvertRouteValueDictionary(innerRouteData));
                        }
                    }

                    returnDict.Add(key, returnList);
                }
                else
                {
                    returnDict.Add(key, value?.ToString());
                }
            }

            return returnDict;
        }
#endif
    }
}
