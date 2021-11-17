// <copyright file="HttpTransport.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext context;

        public HttpTransport(HttpContext context) => this.context = context;

        public bool IsSecureConnection => context.Request.IsHttps;

        public Func<string, string> GetHeader => key => context.Request.Headers[key];

        public IContext GetAdditiveContext()
        {
            return context.Features.Get<IContext>();
        }

        public void SetAdditiveContext(IContext additiveContext)
        {
            context.Features.Set(additiveContext);
            context.Response.RegisterForDispose(additiveContext);
        }

        public IpInfo GetReportedIpInfo()
        {
            var ipAddress = context.Connection.RemoteIpAddress.ToString();
            var port = context.Connection.RemotePort;
            return new IpInfo(ipAddress, port);
        }

        public string GetUserAget()
        {
            return context.Request.Headers[HeaderNames.UserAgent];
        }

        public IHeadersCollection GetRequestHeaders()
        {
            return new HeadersCollectionAdapter(context.Request.Headers);
        }

        public IHeadersCollection GetResponseHeaders()
        {
            return new HeadersCollectionAdapter(context.Response.Headers);
        }

        public void OnCompleted(Action completedCallback)
        {
            context.Response.OnCompleted(() =>
            {
                completedCallback();
                return Task.CompletedTask;
            });
        }

        private readonly struct HeadersCollectionAdapter : IHeadersCollection
        {
            private readonly IHeaderDictionary _headers;

            public HeadersCollectionAdapter(IHeaderDictionary headers)
            {
                _headers = headers;
            }

            public IEnumerable<string> GetValues(string name)
            {
                if (_headers.TryGetValue(name, out var values))
                {
                    return values.ToArray();
                }

                return Enumerable.Empty<string>();
            }

            public void Set(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Add(string name, string value)
            {
                throw new NotImplementedException();
            }

            public void Remove(string name)
            {
                throw new NotImplementedException();
            }
        }
    }
}
#endif
