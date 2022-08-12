// <copyright file="HttpTransport.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class HttpTransport : ITransport
    {
        private const string WafKey = "waf";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpTransport>();
        private readonly HttpContext _context;
        private bool _canReadHttpResponseHeaders = true;

        public HttpTransport(HttpContext context) => _context = context;

        public bool Blocked => false;

        public bool IsSecureConnection => _context.Request.IsSecureConnection;

        public Func<string, string> GetHeader => key => _context.Request.Headers[key];

        public IContext GetAdditiveContext() => _context.Items[WafKey] as IContext;

        public void SetAdditiveContext(IContext additiveContext)
        {
            _context.Items[WafKey] = additiveContext;
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new NameValueHeadersCollection(_context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            if (_canReadHttpResponseHeaders)
            {
                try
                {
                    var headers = _context.Response.Headers;
                    return new NameValueHeadersCollection(_context.Response.Headers);
                }
                catch (PlatformNotSupportedException ex)
                {
                    // Despite the HttpRuntime.UsingIntegratedPipeline check, we can still fail to access response headers, for example when using Sitefinity: "This operation requires IIS integrated pipeline mode"
                    Log.Error(ex, "Unable to access response headers when creating header tags. Disabling for the rest of the application lifetime.");
                    _canReadHttpResponseHeaders = false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting HTTP headers to create header tags.");
                }
            }

            return new NameValueHeadersCollection(new NameValueCollection());
        }

        public void WriteBlockedResponse()
        {
            // todo
        }

        public void StopRequestMovingFurther()
        {
            // todo
        }
    }
}
#endif
