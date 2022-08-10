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
using Datadog.Trace.Configuration;
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

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> PathParamsAvailable;

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> EndRequest;

        public event EventHandler<InstrumentationGatewaySecurityEventArgs> BodyAvailable;

        public event EventHandler<InstrumentationGatewayEventArgs> LastChanceToWriteTags;

        public event EventHandler<InstrumentationGatewayBlockingEventArgs> BlockingOpportunity;

        public void RaiseRequestStart(HttpContext context, HttpRequest request, Span relatedSpan)
        {
            var getEventData = () =>
            {
                var eventData = request.PrepareArgsForWaf();
                eventData.Add(AddressesConstants.ResponseStatus, context.Response.StatusCode.ToString());
                return eventData;
            };

            RaiseEvent(context, relatedSpan, getEventData, EndRequest);
        }

        public void RaisePathParamsAvailable(HttpContext context, Span relatedSpan, IDictionary<string, object> pathParams, bool eraseExistingAddress = true) => RaiseEvent(context, relatedSpan, () => new Dictionary<string, object> { { AddressesConstants.RequestPathParams, pathParams } }, PathParamsAvailable, eraseExistingAddress);

        public void RaiseBodyAvailable(HttpContext context, Span relatedSpan, object body)
        {
            var getEventData = () =>
            {
                var keysAndValues = BodyExtractor.Extract(body);
                var eventData = new Dictionary<string, object>
                {
                    { AddressesConstants.RequestBody, keysAndValues }
                };
                return eventData;
            };

            RaiseEvent(context, relatedSpan, getEventData, BodyAvailable);
        }

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
                    Log.Debug("DDAS-0008-00: Pushing address {Key} to the Instrumentation Gateway.", key);
                }
            }
        }

        private void RaiseEvent(HttpContext context, Span relatedSpan, Func<IDictionary<string, object>> getEventData, EventHandler<InstrumentationGatewaySecurityEventArgs> eventHandler, bool eraseExistingAddress = true)
        {
            if (eventHandler == null)
            {
                return;
            }

            try
            {
                var eventData = getEventData();
                var transport = new HttpTransport(context);
                LogAddressIfDebugEnabled(eventData);
                eventHandler.Invoke(this, new InstrumentationGatewaySecurityEventArgs(eventData, transport, relatedSpan, eraseExistingAddress));
            }
            catch (Exception ex) when (ex is not BlockException)
            {
                Log.Error(ex, "DDAS-0004-00: AppSec failed to process request.");
            }
        }

        internal void RaiseBlockingOpportunity(HttpContext context, Scope scope, ImmutableTracerSettings tracerSettings, Action<InstrumentationGatewayBlockingEventArgs> doBeforeActualBlocking = null)
        {
            if (BlockingOpportunity == null)
            {
                return;
            }

            var transport = new HttpTransport(context);
            BlockingOpportunity.Invoke(this, new InstrumentationGatewayBlockingEventArgs(context, scope, tracerSettings, doBeforeActualBlocking));
        }
    }
}
