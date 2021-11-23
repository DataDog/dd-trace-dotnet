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
using Datadog.Trace.AppSec.Transport.Http;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Serilog.Events;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#endif

namespace Datadog.Trace.AppSec
{
    internal class InstrumentationGateway
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<InstrumentationGateway>();

        public event EventHandler<InstrumentationGatewayEventArgs> InstrumentationGatewayEvent;

        public void RaiseEvent(HttpContext context, HttpRequest request, ISpan relatedSpan, RouteData routeData)
        {
            try
            {
                Dictionary<string, object> eventData = null;
                if (request != null)
                {
                    eventData = request.PrepareArgsForWaf(routeData);
                }
                else if (routeData?.Values?.Count > 0)
                {
                    var routeDataDict = HttpRequestUtils.ConvertRouteValueDictionary(routeData.Values);
                    eventData = new Dictionary<string, object>() { { AddressesConstants.RequestPathParams, routeDataDict } };
                }

                if (eventData != null)
                {
                    var transport = new HttpTransport(context);

                    LogAddressIfDebugEnabled(eventData);

                    InstrumentationGatewayEvent?.Invoke(this, new InstrumentationGatewayEventArgs(eventData, transport, relatedSpan));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AppSec Error.");
            }
        }

        private static void LogAddressIfDebugEnabled(IDictionary<string, object> args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                foreach (var key in args.Keys)
                {
                    Log.Debug("Pushing address {Key} to the Instrumentation Gateway.", key);
                }
            }
        }
    }
}
