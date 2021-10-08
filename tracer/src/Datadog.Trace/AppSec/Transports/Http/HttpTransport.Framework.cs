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

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private const string WafKey = "waf";
        private readonly HttpContext context;

        public HttpTransport(HttpContext context) => this.context = context;

        public void AddRequestScope(Guid guid)
        {
            throw new NotImplementedException();
        }

        public void Block()
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/html";
            context.Response.Write(SecurityConstants.AttackBlockedHtml);
            context.Response.Flush();
            context.ApplicationInstance.CompleteRequest();
        }

        public IContext GetAdditiveContext() => context.Items[WafKey] as IContext;

        public Request Request(string customIpHeader, string[] extraHeaders)
        {
            var request = new Request()
            {
                Url = context.Request.Url.ToString(),
                Method = context.Request.HttpMethod,
                Scheme = context.Request.Url.Scheme,
                Host = context.Request.UserHostName,
            };
            RequestHeadersHelper.FillHeadersAndExtractIpAndPort(key => context.Request.Headers[key], customIpHeader, extraHeaders, context.Request.UserHostAddress, context.Request.IsSecureConnection, request);
            return request;
        }

        public Response Response(bool blocked) => new()
        {
            Status = context.Response.StatusCode,
            Blocked = blocked
        };

        public void SetAdditiveContext(IContext additiveContext)
        {
            context.DisposeOnPipelineCompleted(additiveContext);
            context.Items[WafKey] = additiveContext;
        }

        public void OnCompleted(Action completedCallback)
        {
            context.AddOnRequestCompleted(_ => completedCallback());
        }
    }
}
#endif
