// <copyright file="MultipartFormTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;
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

        public static IEnumerable<object[]> GetTestData() =>
            from useStream in new[] { true, false }
            from useGzip in new[] { true, false }
            select new object[] { useStream, useGzip };

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task ApiWebRequest_MultipartTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunTest(agent, () => factory.Create(url), useStream, useGzip, nameof(ApiWebRequest_MultipartTest));
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task ApiWebRequest_ValidationTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunValidationTest(agent, () => factory.Create(url), useStream, useGzip, nameof(ApiWebRequest_ValidationTest));
        }

#if NETCOREAPP3_1_OR_GREATER

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpClientRequest_MultipartTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunTest(agent, () => factory.Create(url), useStream, useGzip, nameof(HttpClientRequest_MultipartTest));
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpClientRequest_ValidationTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output);
            var url = new Uri($"http://localhost:{agent.Port}/");
            var factory = new HttpClientRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders);
            await RunValidationTest(agent, () => factory.Create(url), useStream, useGzip, nameof(HttpClientRequest_ValidationTest));
        }

#if NET6_0_OR_GREATER
        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpClientRequest_UDS_MultipartTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                AgentHttpHeaderNames.DefaultHeaders,
                Localhost);
            await RunTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(HttpClientRequest_MultipartTest));
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpClientRequest_UDS_ValidationTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                AgentHttpHeaderNames.DefaultHeaders,
                Localhost);
            await RunValidationTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(HttpClientRequest_ValidationTest));
        }
#else
        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpStreamRequest_UDS_MultipartTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new HttpStreamRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
                Localhost);
            await RunTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_MultipartTest));
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpStreamRequest_UDS_ValidationTest(bool useStream, bool useGzip)
        {
            using var agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
            var factory = new HttpStreamRequestFactory(
                new UnixDomainSocketStreamFactory(agent.TracesUdsPath),
                new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
                Localhost);
            await RunValidationTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_ValidationTest));
        }
#endif
#endif

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpStreamRequest_NamedPipes_MultipartTest(bool useStream, bool useGzip)
        {
            if (!EnvironmentTools.IsWindows())
            {
                // Can't use WindowsNamedPipes on non-Windows
                return;
            }

            // named pipes is notoriously flaky
            var attemptsRemaining = 1;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await RunNamedPipesTest();
                    return;
                }
                catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
                {
                }
            }

            async Task RunNamedPipesTest()
            {
                using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
                var factory = new HttpStreamRequestFactory(
                    new NamedPipeClientStreamFactory(agent.TracesWindowsPipeName, timeoutMs: 100),
                    new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
                    Localhost);
                await RunTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_MultipartTest));
            }
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task HttpStreamRequest_NamedPipes_VerificationTest(bool useStream, bool useGzip)
        {
            if (!EnvironmentTools.IsWindows())
            {
                // Can't use WindowsNamedPipes on non-Windows
                return;
            }

            // named pipes is notoriously flaky
            var attemptsRemaining = 1;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await RunNamedPipesTest();
                    return;
                }
                catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
                {
                }
            }

            async Task RunNamedPipesTest()
            {
                using var agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
                var factory = new HttpStreamRequestFactory(
                    new NamedPipeClientStreamFactory(agent.TracesWindowsPipeName, timeoutMs: 100),
                    new DatadogHttpClient(new TraceAgentHttpHeaderHelper()),
                    Localhost);
                await RunValidationTest(agent, () => factory.Create(Localhost), useStream, useGzip, nameof(ApiWebRequest_ValidationTest));
            }
        }

        private async Task RunTest(MockTracerAgent agent, Func<IApiRequest> createRequest, bool useStream, bool useGzip, string snapshotName)
        {
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                requestBody = Encoding.ASCII.GetString(args.Value.ReadStreamBody());
            };

            var request = createRequest();
            var compression = useGzip ? MultipartCompression.GZip : MultipartCompression.None;
            await request.PostAsync(
                [
                    GetItem("Name 1", MimeTypes.Json, "FileName 1.json", useStream),
                    GetItem("Name 2", MimeTypes.MsgPack, "FileName 2.msgpack", useStream)
                ],
                compression);

            Assert.NotNull(requestBody);
            await Verifier.Verify(requestBody)
                          .UseFileName($"{nameof(MultipartFormTests)}.{snapshotName}")
                          .DisableRequireUniquePrefix();
        }

        private async Task RunValidationTest(MockTracerAgent agent, Func<IApiRequest> createRequest, bool useStream, bool useGzip, string snapshotName)
        {
            agent.ShouldDeserializeTraces = false;
            string requestBody = null;
            agent.RequestReceived += (sender, args) =>
            {
                requestBody = Encoding.ASCII.GetString(args.Value.ReadStreamBody());
            };

            var request = createRequest();
            var compression = useGzip ? MultipartCompression.GZip : MultipartCompression.None;
            await request.PostAsync(
                [
                    GetItem("Name\" 1\"", MimeTypes.Json, "FileName 1.json", useStream),
                    GetItem("Name 2", MimeTypes.MsgPack, "FileName '2'.msgpack", useStream)
                ],
                compression);

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
