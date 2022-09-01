// <copyright file="InstrumentationGatewayBlockingEventArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.Configuration;
#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;
#else
using Microsoft.AspNetCore.Http;
#endif

namespace Datadog.Trace.AppSec
{
    internal class InstrumentationGatewayBlockingEventArgs : EventArgs
    {
        internal InstrumentationGatewayBlockingEventArgs(HttpContext context, Scope scope, ImmutableTracerSettings settings, Action<InstrumentationGatewayBlockingEventArgs> doBeforeBlocking = null)
        {
            Context = context;
            Scope = scope;
            TracerSettings = settings;
            DoBeforeBlocking = doBeforeBlocking;
            Transport = new HttpTransport(context);
        }

        internal HttpContext Context { get; }

        internal ITransport Transport { get; }

        internal Scope Scope { get; }

        internal ImmutableTracerSettings TracerSettings { get; }

        private Action<InstrumentationGatewayBlockingEventArgs> DoBeforeBlocking { get; }

        internal void InvokeDoBeforeBlocking()
        {
            if (Scope != null && DoBeforeBlocking != null)
            {
                DoBeforeBlocking(this);
            }
        }
    }
}
