#if !NETSTANDARD2_0

using System;
using System.Web;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.Models
{
    internal class HttpContextTagAdapter : IHttpSpanDecoratable, IHasResourceNameSuffixResolver
    {
        private readonly HttpContext _context;

        private HttpContextTagAdapter(HttpContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static HttpContextTagAdapter Create(HttpContext context) => new HttpContextTagAdapter(context);

        public string GetHeaderValue(string headerName) => _context.Request?.Headers?.Get(headerName);

        public string GetHttpMethod() => _context.Request?.HttpMethod;

        public string GetRawUrl() => _context.Request == null
                                         ? null
                                         : _context.Request.Url?.AbsoluteUri ?? _context.Request.RawUrl;

        public string GetResourceNameSuffix()
            => _context.Request == null
                   ? null
                   : _context.Request.Path ?? _context.Request.RawUrl;
    }
}

#endif
