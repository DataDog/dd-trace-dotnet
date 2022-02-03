// <copyright file="InstrumentationGateway.AspNetCoreOnFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Web.Routing;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.AppSec
{
    internal partial class InstrumentationGateway
    {
        public void RaiseRequestStart(IHttpContext context, IHttpRequest request, Span relatedSpan, IRouteData routeData) => RaiseEvent(context, request, relatedSpan, routeData, RequestStart);

        public void RaiseRequestEnd(IHttpContext context, IHttpRequest request, Span relatedSpan, IRouteData routeData = null) => RaiseEvent(context, request, relatedSpan, routeData, RequestEnd);

        public void RaiseMvcBeforeAction(IHttpContext context, IHttpRequest request, Span relatedSpan, IRouteData routeData = null) => RaiseEvent(context, request, relatedSpan, routeData, MvcBeforeAction);

        public void RaiseLastChanceToWriteTags(IHttpContext context, Span relatedSpan)
        {
            if (LastChanceToWriteTags != null)
            {
                var transport = new HttpTransportCoreOnFramework(context);
                LastChanceToWriteTags.Invoke(this, new InstrumentationGatewayEventArgs(transport, relatedSpan));
            }
        }

        private void RaiseEvent(IHttpContext context, IHttpRequest request, Span relatedSpan, IRouteData routeData, EventHandler<InstrumentationGatewaySecurityEventArgs> eventHandler)
        {
            if (eventHandler == null)
            {
                return;
            }

            try
            {
                Dictionary<string, object> eventData = null;
                if (request != null)
                {
                    eventData = request.PrepareArgsForWaf();
                    eventData.Add(AddressesConstants.ResponseStatus, context.Response.StatusCode.ToString());
                }

                if (routeData?.Values?.Count > 0)
                {
                    var routeDataDict = HttpRequestUtils.ConvertRouteValueDictionary(routeData);
                    eventData = new()
                    {
                        {
                            AddressesConstants.RequestPathParams,
                            routeDataDict
                        }
                    };
                }

                if (eventData != null)
                {
                    var transport = new HttpTransportCoreOnFramework(context);

                    LogAddressIfDebugEnabled(eventData);

                    eventHandler.Invoke(this, new InstrumentationGatewaySecurityEventArgs(eventData, transport, relatedSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AppSec Error.");
            }
        }
    }
}
#endif
