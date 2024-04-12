// <copyright file="MultipartFormTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
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
        private static readonly Uri Localhost = new Uri("http://localhost");
        private readonly ITestOutputHelper _output;

        public MultipartFormTests(ITestOutputHelper output)
        {
            _output = output;
            VerifyHelper.InitializeGlobalSettings();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ApiWebRequest_MultipartTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunTest(agent, () => (IMultipartApiRequest)factory.Create(url), useStream, nameof(ApiWebRequest_MultipartTest));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ApiWebRequest_ValidationTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunValidationTest(agent, () => (IMultipartApiRequest)factory.Create(url), useStream, nameof(ApiWebRequest_ValidationTest));
        }

#if NETCOREAPP3_1_OR_GREATER

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientRequest_MultipartTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunTest(agent, () => (IMultipartApiRequest)factory.Create(url), useStream, nameof(HttpClientRequest_MultipartTest));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientRequest_ValidationTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunValidationTest(agent, () => (IMultipartApiRequest)factory.Create(url), useStream, nameof(HttpClientRequest_ValidationTest));
        }

#if NET6_0_OR_GREATER
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientRequest_UDS_MultipartTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                AgentHttpHeaderNames.DefaultHeaders,
                Localhost);
            await RunTest(agent, () => (IMultipartApiRequest)factory.Create(Localhost), useStream, nameof(HttpClientRequest_MultipartTest));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientRequest_UDS_ValidationTest(bool useStream)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                AgentHttpHeaderNames.DefaultHeaders,
                Localhost);
            await RunValidationTest(agent, () => (IMultipartApiRequest)factory.Create(Localhost), useStream, nameof(HttpClientRequest_ValidationTest));
        }
#endif
#endif

        private async Task RunTest(MockTracerAgent agent, Func<IMultipartApiRequest> createRequest, bool useStream, string snapshotName)
        {
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                requestBody = Encoding.ASCII.GetString(args.Value.ReadStreamBody());
            };

            var request = createRequest();
            await request.PostAsync(new MultipartFormItem[]
            {
                GetItem("Name 1", MimeTypes.Json, "FileName 1.json", useStream),
                GetItem("Name 2", MimeTypes.MsgPack, "FileName 2.msgpack", useStream),
            });

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody)
                          .UseFileName($"{nameof(MultipartFormTests)}.{snapshotName}")
                          .DisableRequireUniquePrefix();
        }

        private async Task RunValidationTest(MockTracerAgent agent, Func<IMultipartApiRequest> createRequest, bool useStream, string snapshotName)
        {
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                requestBody = Encoding.ASCII.GetString(args.Value.ReadStreamBody());
            };

            var request = createRequest();
            await request.PostAsync(new MultipartFormItem[]
            {
                GetItem("Name\" 1\"", MimeTypes.Json, "FileName 1.json", useStream),
                GetItem("Name 2", MimeTypes.MsgPack, "FileName '2'.msgpack", useStream),
            });

            var emptyRequest = "--faa0a896-8bc8-48f3-b46d-016f2b15a884\r\n\r\n--faa0a896-8bc8-48f3-b46d-016f2b15a884--\r\n";
            Assert.Equal(emptyRequest, requestBody);

            requestBody = null;
            request = createRequest();
            await request.PostAsync(new MultipartFormItem[]
            {
                GetItem("Name\" 1\"", MimeTypes.Json, "FileName 1.json", useStream),
                GetItem("Name 2", MimeTypes.MsgPack, "FileName2.msgpack", useStream),
            });

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody)
                          .UseFileName($"{nameof(MultipartFormTests)}.{snapshotName}")
                          .DisableRequireUniquePrefix();
        }

        private MultipartFormItem GetItem(string name, string contentType, string fileName, bool useStream)
            => useStream
                   ? new(name, contentType, fileName, new MemoryStream([42]))
                   : new(name, contentType, fileName, new ArraySegment<byte>([42]));
    }
}
