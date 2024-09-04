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

            var security = Security.Instance;
            var runIast = Iast.Iast.Instance.Settings.Enabled;
            IastRequestContext? iastRequestContext = null;
            if (runIast)
            {
                iastRequestContext = scope.Span?.Context?.TraceContext?.IastRequestContext;
                runIast = iastRequestContext is not null;
            }

            // if neither iast or security is enabled leave
            if (!security.Enabled && !runIast)
            {
                return;
            }

            var bodyDic = new Dictionary<string, object>();
            var pathParamsDic = new Dictionary<string, object>();
            foreach (var item in parameters)
            {
                if (controllerContext.RouteData?.Values?.ContainsKey(item.Key) ?? false)
                {
                    pathParamsDic[item.Key] = item.Value;
                }
                else
                {
                    // We exclude the query string params
                    if (!RequestDataHelper.GetQueryString(context.Request)?.AllKeys.Contains(item.Key) ?? false)
                    {
                        bodyDic[item.Key] = item.Value;
                    }
                }
            }

            object? requestBody = null;
            if (bodyDic.Count > 0)
            {
                requestBody = ObjectExtractor.Extract(bodyDic);
            }

            if (security.Enabled)
            {
                var securityTransport = new Coordinator.SecurityCoordinator(security, scope.Span!);
                if (!securityTransport.IsBlocked)
                {
                    var inputData = new Dictionary<string, object>();
                    if (requestBody is not null)
                    {
                        inputData.Add(AddressesConstants.RequestBody, requestBody);
                    }

                    if (pathParamsDic.Count > 0)
                    {
                        var pathParams = ObjectExtractor.Extract(pathParamsDic);

                        if (pathParams is not null)
                        {
                            inputData.Add(AddressesConstants.RequestPathParams, pathParams);
                        }
                    }

                    securityTransport.BlockAndReport(inputData);
                }
            }

            if (runIast)
            {
                if (controllerContext.RouteData?.Values?.Count > 0)
                {
                    iastRequestContext!.AddRequestData(context.Request, controllerContext.RouteData.Values);
                }

                if (requestBody is not null)
                {
                    iastRequestContext!.AddRequestBody(null, requestBody);
                }
            }
        }
    }
}
#endif
