// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using Datadog.Trace.DiagnosticListeners;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext context;

        public HttpTransport(HttpContext context)
        {
            this.context = context;
        }

        public void Block()
        {
            if (context.Items.ContainsKey(SecurityConstants.InHttpPipeKey) && context.Items[SecurityConstants.InHttpPipeKey] is bool inHttpPipe && inHttpPipe)
            {
                throw new BlockActionException();
            }
            else
            {
                context.Items[SecurityConstants.KillKey] = true;
            }
        }
    }
}
#endif
