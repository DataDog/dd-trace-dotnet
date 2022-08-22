// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class HttpTransport : ITransport
    {
        private static System.Reflection.MethodInfo _completeAsync;

        private readonly HttpContext _context;

        public HttpTransport(HttpContext context) => _context = context;

        public bool IsSecureConnection => _context.Request.IsHttps;

        public bool Blocked => _context.Items["block"] != null;

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

        public void WriteBlockedResponse(string templateJson, string templateHtml)
        {
            _context.Items["block"] = true;
            var httpResponse = _context.Response;
            httpResponse.Clear();
            httpResponse.StatusCode = 403;
            if (_context.Request.Headers["Accept"] == "application/json")
            {
                httpResponse.WriteAsync(templateJson).Wait();
                httpResponse.ContentType = "application/json";
            }
            else
            {
                httpResponse.WriteAsync(templateHtml).Wait();
                httpResponse.ContentType = "text/html";
            }

            _completeAsync ??= httpResponse.GetType().GetMethod("CompleteAsync");
            if (_completeAsync != null)
            {
                var t = (Task)_completeAsync.Invoke(httpResponse, null);
                t.ConfigureAwait(false);
                t.Wait();
            }
        }

        public void DisposeContextInTheEnd()
        {
            var context = GetAdditiveContext();
            _context.Response.RegisterForDispose(context);
        }
    }
}
#endif
