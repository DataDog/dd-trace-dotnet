// <copyright file="MultipartFormTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
    [UsesVerify]
    public class MultipartFormTests
    {
        public MultipartFormTests()
        {
            VerifyHelper.InitializeGlobalSettings();
        }

        [Fact]
        public async Task ApiWebRequestMultipartTest()
        {
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                var ctx = args.Value;
                var rq = ctx.Request;
                using var sreader = new StreamReader(rq.InputStream, Encoding.ASCII);
                requestBody = sreader.ReadToEnd();
            };

            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            var request = (IMultipartApiRequest)factory.Create(url);
            await request.PostAsync(new MultipartFormItem[]
            {
                new("Name 1", MimeTypes.Json, "FileName 1.json", new ArraySegment<byte>(new byte[] { 42 })),
                new("Name 2", MimeTypes.MsgPack, "FileName 2.msgpack", new ArraySegment<byte>(new byte[] { 42 })),
            });

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody);
        }

        [Fact]
        public async Task ApiWebRequestValidationTest()
        {
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                var ctx = args.Value;
                var rq = ctx.Request;
                using var sreader = new StreamReader(rq.InputStream, Encoding.ASCII);
                requestBody = sreader.ReadToEnd();
            };

            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            var request = (IMultipartApiRequest)factory.Create(url);
            await request.PostAsync(new MultipartFormItem[]
            {
                new("Name\" 1\"", MimeTypes.Json, "FileName 1.json", new ArraySegment<byte>(new byte[] { 42 })),
                new("Name 2", MimeTypes.MsgPack, "FileName '2'.msgpack", new ArraySegment<byte>(new byte[] { 42 })),
            });

            Assert.Equal(string.Empty, requestBody);

            requestBody = null;
            request = (IMultipartApiRequest)factory.Create(url);
            await request.PostAsync(new MultipartFormItem[]
            {
                new("Name\" 1\"", MimeTypes.Json, "FileName 1.json", new ArraySegment<byte>(new byte[] { 42 })),
                new("Name 2", MimeTypes.MsgPack, "FileName2.msgpack", new ArraySegment<byte>(new byte[] { 42 })),
            });

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody);
        }
#if NETCOREAPP3_1_OR_GREATER

        [Fact]
        public async Task HttpClientRequestMultipartTest()
        {
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                var ctx = args.Value;
                var rq = ctx.Request;
                using var sreader = new StreamReader(rq.InputStream, Encoding.ASCII);
                requestBody = sreader.ReadToEnd();
            };

            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            var request = (IMultipartApiRequest)factory.Create(url);
            await request.PostAsync(new MultipartFormItem[]
            {
                new("Name 1", MimeTypes.Json, "FileName 1.json", new ArraySegment<byte>(new byte[] { 42 })),
                new("Name 2", MimeTypes.MsgPack, "FileName 2.msgpack", new ArraySegment<byte>(new byte[] { 42 })),
            });

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody);
        }
#endif
    }
}
