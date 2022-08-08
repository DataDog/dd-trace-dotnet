// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        public bool IsSecureConnection => _context.Request.IsHttps;

        public bool Blocked => _context.Items["blocked"] != null;

        public Func<string, string> GetHeader => key => _context.Request.Headers[key];

        public IContext GetAdditiveContext()
        {
            return _context.Features.Get<IContext>();
        }

        public void SetAdditiveContext(IContext additive_context)
        {
            _context.Features.Set(additive_context);
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new HeadersCollectionAdapter(_context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new HeadersCollectionAdapter(_context.Response.Headers);
        }

        public void Block()
        {
            var httpResponse = _context.Response;
            httpResponse.StatusCode = 403;
            httpResponse.ContentType = "text/html";
            httpResponse.WriteAsync(SecurityConstants.AttackBlockedHtml).Wait();
            var method = httpResponse.GetType().GetMethod("CompleteAsync");
            if (method != null)
            {
                var t = (Task)method.Invoke(httpResponse, null);
                t.ConfigureAwait(false);
                t.Wait();
            }
        }
    }
}
#endif
