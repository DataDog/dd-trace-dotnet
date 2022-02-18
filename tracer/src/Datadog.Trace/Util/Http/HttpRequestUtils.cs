// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Web.Routing;
#endif
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.Util.Http
{
    internal static class HttpRequestUtils
    {
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

        internal static object ConvertMvcPameters(IDictionary<string, object> parameters)
        {
            return parameters.ToDictionary(kvp => kvp.Key, kvp => ConvertObject(kvp.Value));
        }

        private static object ConvertObject(object stateObj)
        {
            var t = stateObj.GetType();
            var props = t.GetProperties().Where(x => x.CanRead && x.GetIndexParameters().Length == 0);

            var result = new Dictionary<string, object>();

            foreach (var prop in props)
            {
                // TODO need an heuristic to decide if this is a nested object
                var value = prop.GetValue(stateObj)?.ToString() ?? string.Empty;
                result.Add(prop.Name, value);
            }

            return result;
        }

        private static object ConvertRouteValueList(List<RouteData> routeDataList)
        {
            return routeDataList.Select(x => ConvertRouteValueDictionary(x.Values)).ToList();
        }
    }
}
