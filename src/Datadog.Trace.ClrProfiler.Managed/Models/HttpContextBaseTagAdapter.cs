#if !NETSTANDARD2_0

using System;
using System.Web;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal class HttpContextBaseTagAdapter : IHttpSpanDecoratable
    {
        private readonly HttpContextBase _context;

        private HttpContextBaseTagAdapter(HttpContextBase context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static HttpContextBaseTagAdapter Create(HttpContextBase context) => new HttpContextBaseTagAdapter(context);

        public string GetHeaderValue(string headerName) => _context.Request?.Headers?.Get(headerName);

        public string GetHttpMethod() => _context.Request?.HttpMethod;

        public string GetRawUrl() => _context.Request == null
                                         ? null
                                         : _context.Request.Url?.AbsoluteUri ?? _context.Request.RawUrl;
    }
}

#endif
