// <copyright file="HttpTransportCoreOnFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Specialized;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

using IHttpContext = Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.IHttpContext;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class HttpTransportCoreOnFramework : ITransport
    {
        private readonly IHttpContext _context;

        public HttpTransportCoreOnFramework(IHttpContext context) => _context = context;

        public bool IsSecureConnection => _context.Request.IsHttps;

        public Func<string, string> GetHeader => key => _context.Request.Headers.GetItemAsString(key);

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

        public IHeadersCollection GetRequestHeaders()
        {
            return new IHeaderDictionaryHeadersCollection(_context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new IHeaderDictionaryHeadersCollection(_context.Response.Headers);
        }
    }
}
#endif
