// <copyright file="InstrumentationGateway.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;
#endif
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Serilog.Events;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.AppSec
{
    internal partial class InstrumentationGateway
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<InstrumentationGateway>();

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> RequestStart;

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> MvcBeforeAction;

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> RequestEnd;

        public event EventHandler<InstrumentationGatewayEventArgs> LastChanceToWriteTags;

        public void RaiseRequestStart(HttpContext context, HttpRequest request, Span relatedSpan, RouteData routeData) => RaiseEvent(context, request, relatedSpan, routeData, RequestStart);

        public void RaiseRequestEnd(HttpContext context, HttpRequest request, Span relatedSpan, RouteData routeData = null) => RaiseEvent(context, request, relatedSpan, routeData, RequestEnd);

        public void RaiseMvcBeforeAction(HttpContext context, HttpRequest request, Span relatedSpan, RouteData routeData = null) => RaiseEvent(context, request, relatedSpan, routeData, MvcBeforeAction);

        public void RaiseLastChanceToWriteTags(HttpContext context, Span relatedSpan)
        {
            if (LastChanceToWriteTags != null)
            {
                var transport = new HttpTransport(context);
                LastChanceToWriteTags.Invoke(this, new InstrumentationGatewayEventArgs(transport, relatedSpan));
            }
        }

        private static void LogAddressIfDebugEnabled(IDictionary<string, object> args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                foreach (var key in args.Keys)
                {
                    Log.Debug("DDAS-0009-00: Pushing address {Key} to the Instrumentation Gateway.", key);
                }
            }
        }

        private void RaiseEvent(HttpContext context, HttpRequest request, Span relatedSpan, RouteData routeData, EventHandler<InstrumentationGatewaySecurityEventArgs> eventHandler)
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
                    var routeDataDict = HttpRequestUtils.ConvertRouteValueDictionary(routeData.Values);
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
                    var transport = new HttpTransport(context);

                    LogAddressIfDebugEnabled(eventData);

                    eventHandler.Invoke(this, new InstrumentationGatewaySecurityEventArgs(eventData, transport, relatedSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DDAS-0004-00: AppSec failed to process request.");
            }
        }
    }
}
