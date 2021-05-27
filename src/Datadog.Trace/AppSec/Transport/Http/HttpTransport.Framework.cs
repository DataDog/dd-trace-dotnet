// <copyright file="HttpTransport.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Web;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly System.Web.HttpContext context;

        public HttpTransport(HttpContext context)
        {
            this.context = context;
        }

        public void Block()
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/html";
            context.Response.Write(SecurityConstants.AttackBlockedHtml);
            context.Response.Flush();
            context.Response.End();
        }
    }
}
#endif
