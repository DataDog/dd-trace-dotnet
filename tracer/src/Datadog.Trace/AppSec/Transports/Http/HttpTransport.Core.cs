// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        public bool IsSecureConnection => _context.Request.IsHttps;

        public Func<string, string> GetHeader => key => _context.Request.Headers[key];

        public IContext GetAdditiveContext()
        {
            return _context.Features.Get<IContext>();
        }

        public void SetAdditiveContext(IContext additive_context)
        {
            _context.Features.Set(additive_context);
            _context.Response.RegisterForDispose(additive_context);
        }

        public IpInfo GetReportedIpInfo()
        {
            var ipAddress = _context.Connection.RemoteIpAddress.ToString();
            var port = _context.Connection.RemotePort;
            return new IpInfo(ipAddress, port);
        }

        public string GetUserAgent()
        {
            return _context.Request.Headers[HeaderNames.UserAgent];
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new HeadersCollectionAdapter(_context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new HeadersCollectionAdapter(_context.Response.Headers);
        }

        public void OnCompleted(Action completedCallback)
        {
            _context.Response.OnCompleted(() =>
            {
                completedCallback();
                return Task.CompletedTask;
            });
        }
    }
}
#endif
