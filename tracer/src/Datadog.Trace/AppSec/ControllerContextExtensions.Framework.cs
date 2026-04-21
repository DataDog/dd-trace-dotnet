// <copyright file="ControllerContextExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.Iast;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec
{
    internal static class ControllerContextExtensions
    {
        internal static void MonitorBodyAndPathParams(this IControllerContext controllerContext, IDictionary<string, object>? parameters, string peekScopeKey)
        {
            if (parameters is null or { Count: 0 })
            {
                return;
            }

            var context = HttpContext.Current;
            if (context is null)
            {
                return;
            }

            var scope = SharedItems.TryPeekScope(context, peekScopeKey);
            if (scope == null)
            {
                return;
            }

            if (!Iast.Iast.Instance.Settings.Enabled)
            {
                return;
            }

            var iastRequestContext = scope.Span?.Context?.TraceContext?.IastRequestContext;
            if (iastRequestContext is null)
            {
                return;
            }

            var bodyDic = new Dictionary<string, object>();
            foreach (var item in parameters)
            {
                if (controllerContext.RouteData?.Values?.ContainsKey(item.Key) ?? false)
                {
                    continue;
                }

                if (!RequestDataHelper.GetQueryString(context.Request)?.AllKeys.Contains(item.Key) ?? false)
                {
                    bodyDic[item.Key] = item.Value;
                }
            }

            object? requestBody = null;
            if (bodyDic.Count > 0)
            {
                requestBody = ObjectExtractor.Extract(bodyDic);
            }

            if (controllerContext.RouteData?.Values?.Count > 0)
            {
                iastRequestContext.AddRequestData(context.Request, controllerContext.RouteData.Values);
            }

            if (requestBody is not null)
            {
                iastRequestContext.AddRequestBody(null, requestBody);
            }
        }
    }
}
#endif
