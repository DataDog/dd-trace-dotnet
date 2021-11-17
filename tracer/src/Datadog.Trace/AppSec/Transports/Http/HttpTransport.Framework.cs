// <copyright file="HttpTransport.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private const string WafKey = "waf";
        private readonly HttpContext context;

        public HttpTransport(HttpContext context) => this.context = context;

        public bool IsSecureConnection => context.Request.IsSecureConnection;

        public Func<string, string> GetHeader => key => context.Request.Headers[key];

        public IContext GetAdditiveContext() => context.Items[WafKey] as IContext;

        public void SetAdditiveContext(IContext additiveContext)
        {
            context.DisposeOnPipelineCompleted(additiveContext);
            context.Items[WafKey] = additiveContext;
        }

        public IpInfo GetReportedIpInfo()
        {
            var hostAddress = context.Request.UserHostAddress;
            var isSecure = context.Request.IsSecureConnection;
            return IpExtractor.ExtractAddressAndPort(hostAddress, isSecure);
        }

        public string GetUserAget()
        {
            return context.Request.UserAgent;
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new NameValueHeadersCollection(context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new NameValueHeadersCollection(context.Response.Headers);
        }

        public void OnCompleted(Action completedCallback)
        {
            context.AddOnRequestCompleted(_ => completedCallback());
        }
    }
}
#endif
