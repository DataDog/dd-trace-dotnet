// <copyright file="OwinFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Starts the <c>Samples.Owin.WebApi2</c> sample, which self-hosts Web API with OWIN rather than
    /// running under IIS, and keeps it alive for every test case in a class. Shared by
    /// <see cref="OwinWebApi2Tests"/> and <see cref="OtlpOwinWebApi2Tests"/>; each test class
    /// gets its own instance, and so its own process started with that class's configuration.
    /// </summary>
    public sealed class OwinFixture : IDisposable
    {
        private readonly HttpClient _httpClient;
        private Process _process;

        public OwinFixture()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("baggage", "user.id=doggo");
        }

        public MockTracerAgent.TcpUdpAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        /// <summary>
        /// Gets the ddapm test-agent session the sample exports OTLP to, for suites that use
        /// <c>OTEL_TRACES_EXPORTER=otlp</c>. Owned by the fixture rather than by each test case for the
        /// same reason as <c>IisFixture.OtlpSession</c>: the session token is baked into the sample's
        /// <c>OTEL_EXPORTER_OTLP_HEADERS</c> when the process starts, and that process is shared by
        /// every test case in the class.
        /// </summary>
        internal OtlpTestAgentSession OtlpSession { get; } = new();

        public async Task TryStartApp(TestHelper helper, ITestOutputHelper output)
        {
            if (_process is not null)
            {
                return;
            }

            if (_process is null)
            {
                var initialAgentPort = TcpPortProvider.GetOpenPort();
                HttpPort = TcpPortProvider.GetOpenPort();

                Agent = MockTracerAgent.Create(output, initialAgentPort);
                Agent.SpanFilters.Add(IsNotServerLifeCheck);
                output.WriteLine($"Starting OWIN sample, agentPort: {Agent.Port}, samplePort: {HttpPort}");
                _process = await helper.StartSample(Agent, arguments: null, packageVersion: string.Empty, aspNetCorePort: HttpPort);
            }

            await EnsureServerStarted(output);
        }

        public void Dispose()
        {
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        SubmitRequest(null, "/shutdown").GetAwaiter().GetResult();

                        _process.Kill();
                    }
                }
                catch
                {
                    // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                }

                _process.Dispose();
            }

            Agent?.Dispose();
        }

        public async Task<IImmutableList<MockSpan>> WaitForSpans(ITestOutputHelper output, string path, int expectedSpanCount)
        {
            var testStart = DateTimeOffset.UtcNow;

            await SubmitRequest(output, path);
            return await Agent.WaitForSpansAsync(count: expectedSpanCount, minDateTime: testStart, returnAllOperations: true);
        }

        private async Task EnsureServerStarted(ITestOutputHelper output)
        {
            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    if (args.Data.Contains("Webserver started"))
                    {
                        wh.Set();
                    }

                    output.WriteLine($"[webserver][stdout] {args.Data}");
                }
            };
            _process.BeginOutputReadLine();

            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.WriteLine($"[webserver][stderr] {args.Data}");
                }
            };

            _process.BeginErrorReadLine();

            wh.WaitOne(5000);

            var maxMillisecondsToWait = 30_000;
            var intervalMilliseconds = 500;
            var intervals = maxMillisecondsToWait / intervalMilliseconds;
            var serverReady = false;

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                try
                {
                    serverReady = await SubmitRequest(output, "/alive-check") == HttpStatusCode.OK;
                }
                catch
                {
                    // ignore
                }

                if (serverReady)
                {
                    break;
                }

                await Task.Delay(intervalMilliseconds);
            }

            if (!serverReady)
            {
                throw new Exception("Couldn't verify the application is ready to receive requests.");
            }
        }

        private bool IsNotServerLifeCheck(MockSpan span)
        {
            span.Tags.TryGetValue(Tags.HttpUrl, out var url);
            if (url == null)
            {
                return true;
            }

            return !url.Contains("alive-check") && !url.Contains("shutdown");
        }

        private async Task<HttpStatusCode> SubmitRequest(ITestOutputHelper output, string path)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"http://localhost:{HttpPort}{path}");
            string responseText = await response.Content.ReadAsStringAsync();
            output?.WriteLine($"[http] {response.StatusCode} {responseText}");
            return response.StatusCode;
        }
    }
}
#endif
