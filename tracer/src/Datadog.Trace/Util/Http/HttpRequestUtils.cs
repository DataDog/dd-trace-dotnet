// <copyright file="HttpRequestUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Web.Routing;
#else
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.Util.Http
{
    internal static class HttpRequestUtils
    {
        internal static IDictionary<string, object> ConvertRouteValueDictionary(RouteValueDictionary routeDataDict)
        {
            var list = new Dictionary<string, object>(routeDataDict.Count);
            foreach (var keyValuePair in routeDataDict)
            {
                list.Add(keyValuePair.Key, keyValuePair.Value?.ToString());
            }

            return list;
        }
    }
}
