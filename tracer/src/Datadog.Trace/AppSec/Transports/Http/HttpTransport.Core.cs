// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext context;

        public HttpTransport(HttpContext context) => this.context = context;

        public bool IsSecureConnection => context.Request.IsHttps;

        public Func<string, string> GetHeader => key => context.Request.Headers[key];

        public Request Request()
        {
            var request = new Request
            {
                Method = context.Request.Method,
                Path = context.Request.Path,
                Scheme = context.Request.Scheme,
                Url = context.Request.GetUrl()
            };

            if (context.Request.Host.HasValue)
            {
                request.Host = context.Request.Host.ToString();
                request.RemoteIp = context.Connection.RemoteIpAddress.ToString();
                request.Port = context.Connection.RemotePort;
            }

            return request;
        }

        public Response Response(bool blocked) => new Response
        {
            Status = context.Response.StatusCode,
            Blocked = blocked
        };

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

        public IContext GetAdditiveContext()
        {
            return context.Features.Get<IContext>();
        }

        public void SetAdditiveContext(IContext additiveContext)
        {
            context.Features.Set(additiveContext);
            context.Response.RegisterForDispose(additiveContext);
        }

        public void AddRequestScope(Guid guid) => context.Items.Add("Security", guid);

        public void OnCompleted(Action completedCallback)
        {
            context.Response.OnCompleted(() =>
            {
                completedCallback();
                return Task.CompletedTask;
            });
        }
    }
}
#endif
