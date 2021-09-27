// <copyright file="HttpRequestExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.DataFormat;
#if NETFRAMEWORK
using System.Web.Routing;
#endif
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.Util.Http
{
    internal static partial class HttpRequestExtensions
    {
        private static Node ConvertRouteValueDictionary(RouteValueDictionary routeDataDict)
        {
            var dict = routeDataDict.ToDictionary(
                c => c.Key,
                c =>
                    c.Value switch
                    {
                        List<RouteData> routeDataList => ConvertRouteValueList(routeDataList),
                        _ => Node.NewString(c.Value?.ToString())
                    });

            return Node.NewMap(dict);
        }

        private static Node ConvertRouteValueList(List<RouteData> routeDataList)
        {
             var list = routeDataList.Select(x => ConvertRouteValueDictionary(x.Values)).ToList();
             return Node.NewList(list);
        }
    }
}
