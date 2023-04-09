// <copyright file="ControllerContextExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast;

namespace Datadog.Trace.AppSec
{
    internal static class ControllerContextExtensions
    {
        internal static void MonitorBodyAndPathParams(this IControllerContext controllerContext, IDictionary<string, object> parameters, string peekScopeKey)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return;
            }

            var security = Security.Instance;
            var context = HttpContext.Current;
            var iastEnabled = Iast.Iast.Instance.Settings.Enabled;
            Scope scope = null;

            if ((context != null && security.Settings.Enabled) || iastEnabled)
            {
                scope = SharedItems.TryPeekScope(context, peekScopeKey);
            }

            if (context != null && security.Settings.Enabled)
            {
                var bodyDic = new Dictionary<string, object>(parameters.Count);
                var pathParamsDic = new Dictionary<string, object>(parameters.Count);
                foreach (var item in parameters)
                {
                    if (controllerContext.RouteData.Values.ContainsKey(item.Key))
                    {
                        pathParamsDic[item.Key] = item.Value;
                    }
                    else
                    {
                        bodyDic[item.Key] = item.Value;
                    }
                }

                var securityTransport = new Coordinator.SecurityCoordinator(security, context, scope.Span);
                if (!securityTransport.IsBlocked)
                {
                    var inputData = new Dictionary<string, object>
                    {
                        { AddressesConstants.RequestBody, ObjectExtractor.Extract(bodyDic) },
                        { AddressesConstants.RequestPathParams, ObjectExtractor.Extract(pathParamsDic) }
                    };
                    securityTransport.CheckAndBlock(inputData);
                }
            }

            if (iastEnabled)
            {
                scope?.Span?.Context?.TraceContext?.IastRequestContext?.AddRequestData(context.Request, controllerContext.RouteData.Values);
            }
        }
    }
}
#endif
