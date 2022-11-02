// <copyright file="SecurityTransport.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Transports.Http;
#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec
{
    internal partial class SecurityTransport
    {
        private readonly HttpContext _context;

        public SecurityTransport(Security security, HttpContext context, Span span)
        {
            _security = security;
            _context = context;
            _span = span;
            _transport = new HttpTransport(context);
        }

        internal IResult ShouldBlockBody()
        {
            var formData = new Dictionary<string, object>();
            foreach (string key in _context.Request.Form.Keys)
            {
                formData.Add(key, _context.Request.Form[key]);
            }

            var args = new Dictionary<string, object> { { AddressesConstants.RequestBody, formData } };
            return RunWaf(args);
        }

        internal IResult ShouldBlockPathParams()
        {
            var args = new Dictionary<string, object> { { AddressesConstants.RequestPathParams, _context.Request.RequestContext.RouteData.Values } };
            return RunWaf(args);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityTransport"/> class.
        /// framework can do it all at once, but framework only unfortunately
        /// </summary>
        internal void CheckAndBlock(IResult result)
        {
            if (result.ReturnCode >= ReturnCode.Match)
            {
                if (result.ReturnCode == ReturnCode.Block)
                {
                    WriteAndEndResponse();
                    Report(result, true);
                }

                AddResponseHeaderTags();
            }
        }

        public void WriteAndEndResponse()
        {
            var httpResponse = _context.Response;
            httpResponse.Clear();
            httpResponse.Cookies.Clear();

            if (_security.CanAccessHeaders())
            {
                var keys = httpResponse.Headers.Keys.Cast<string>().ToList();
                foreach (var key in keys)
                {
                    httpResponse.Headers.Remove(key);
                }
            }

            httpResponse.StatusCode = 403;

            var template = _security.Settings.BlockedJsonTemplate;
            if (_context.Request.Headers["Accept"] == "application/json")
            {
                httpResponse.ContentType = "application/json";
            }
            else
            {
                httpResponse.ContentType = "text/html";
                template = _security.Settings.BlockedHtmlTemplate;
            }

            httpResponse.Write(template);
            httpResponse.Flush();
            httpResponse.Close();
            _context.ApplicationInstance.CompleteRequest();
        }
    }
}
#endif
