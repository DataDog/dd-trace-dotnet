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
        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        public bool IsSecureConnection => _context.Request.IsSecureConnection;

        public Func<string, string> GetHeader => key => _context.Request.Headers[key];

        public IContext GetAdditiveContext() => _context.Items[WafKey] as IContext;

        public void SetAdditiveContext(IContext additiveContext)
        {
            _context.DisposeOnPipelineCompleted(additiveContext);
            _context.Items[WafKey] = additiveContext;
        }

        public IpInfo GetReportedIpInfo()
        {
            var hostAddress = _context.Request.UserHostAddress;
            var isSecure = _context.Request.IsSecureConnection;
            return IpExtractor.ExtractAddressAndPort(hostAddress, isSecure);
        }

        public string GetUserAget()
        {
            return _context.Request.UserAgent;
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new NameValueHeadersCollection(_context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new NameValueHeadersCollection(_context.Response.Headers);
        }

        public void OnCompleted(Action completedCallback)
        {
            _context.AddOnRequestCompleted(_ => completedCallback());
        }
    }
}
#endif
