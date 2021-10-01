// <copyright file="HttpClientRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using NUnit.Framework;

namespace Datadog.Trace.Tests
{
    public class HttpClientRequestTests
    {
        [Test]
        public async Task SetHeaders()
        {
            var handler = new CustomHandler();

            var factory = new HttpClientRequestFactory(handler);
            var request = factory.Create(new Uri("http://localhost/"));

            request.AddHeader("Hello", "World");

            await request.PostAsync(ArraySegment<byte>.Empty);

            var message = handler.Message;

            Assert.NotNull(message);
            Assert.AreEqual(".NET", message.Headers.GetValues(AgentHttpHeaderNames.Language).First());
            Assert.AreEqual(TracerConstants.AssemblyVersion, message.Headers.GetValues(AgentHttpHeaderNames.TracerVersion).First());
            Assert.AreEqual("false", message.Headers.GetValues(HttpHeaderNames.TracingEnabled).First());
            Assert.AreEqual("World", message.Headers.GetValues("Hello").First());
        }

        [Test]
        public async Task SerializeSpans()
        {
            var handler = new CustomHandler();

            var factory = new HttpClientRequestFactory(handler);
            var request = factory.Create(new Uri("http://localhost/"));

            await request.PostAsync(ArraySegment<byte>.Empty);

            var message = handler.Message;

            Assert.IsAssignableFrom<ByteArrayContent>(message.Content);
        }

        private class CustomHandler : DelegatingHandler
        {
            public HttpRequestMessage Message { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Message = request;
                return Task.FromResult(new HttpResponseMessage());
            }
        }
    }
}

#endif
