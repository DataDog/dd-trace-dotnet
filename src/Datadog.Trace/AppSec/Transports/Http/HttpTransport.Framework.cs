// <copyright file="HttpTransport.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private const string WafKey = "waf";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpTransport>();
        private readonly System.Web.HttpContext context;

        public HttpTransport(HttpContext context)
        {
            this.context = context;
        }

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

        public IAdditiveContext GetAdditiveContext()
        {
            return context.Items[WafKey] as IAdditiveContext;
        }

        public Request Request()
        {
            return new Request
            {
                Url = context.Request.Url,
                Method = context.Request.HttpMethod,
            };
        }

        public Response Response(bool blocked) => new()
        {
            Status = context.Response.StatusCode,
            Blocked = blocked
        };

        public void SetAdditiveContext(IAdditiveContext additiveContext)
        {
            context.DisposeOnPipelineCompleted(additiveContext);
            context.Items[WafKey] = additiveContext;
        }
    }
}
#endif
