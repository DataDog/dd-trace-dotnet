// <copyright file="OwinTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNet
{
    [NonParallelizable]
    public abstract class OwinTestsBase : TestHelper
    {
        private readonly HttpClient _httpClient;
        private Process _process;

        protected OwinTestsBase(string sampleAppName)
            : base(sampleAppName)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public MockTracerAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        [OneTimeSetUp]
        public async Task TryStartApp()
        {
            if (_process is not null)
            {
                return;
            }

            lock (this)
            {
                if (_process is null)
                {
                    var initialAgentPort = TcpPortProvider.GetOpenPort();
                    HttpPort = TcpPortProvider.GetOpenPort();

                    Agent = new MockTracerAgent(initialAgentPort);
                    Agent.SpanFilters.Add(IsNotServerLifeCheck);
                    Console.WriteLine($"Starting OWIN sample, agentPort: {Agent.Port}, samplePort: {HttpPort}");
                    _process = StartSample(Agent.Port, arguments: null, packageVersion: string.Empty, aspNetCorePort: HttpPort);
                }
            }

            await EnsureServerStarted();
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            lock (this)
            {
                if (_process is not null)
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            SubmitRequest("/shutdown").GetAwaiter().GetResult();

                            _process.Kill();
                        }
                    }
                    catch
                    {
                        // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                    }

                    _process.Dispose();
                }
            }

            Agent?.Dispose();
        }

        public async Task<IImmutableList<MockTracerAgent.Span>> WaitForSpans(string path, int expectedSpanCount)
        {
            var testStart = DateTime.UtcNow;

            await SubmitRequest(path);
            return Agent.WaitForSpans(count: expectedSpanCount, minDateTime: testStart, returnAllOperations: true);
        }

        private async Task EnsureServerStarted()
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

                    Console.WriteLine($"[webserver][stdout] {args.Data}");
                }
            };
            _process.BeginOutputReadLine();

            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Console.WriteLine($"[webserver][stderr] {args.Data}");
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
                    serverReady = await SubmitRequest("/alive-check") == HttpStatusCode.OK;
                }
                catch
                {
                    // ignore
                }

                if (serverReady)
                {
                    break;
                }

                Thread.Sleep(intervalMilliseconds);
            }

            if (!serverReady)
            {
                throw new Exception("Couldn't verify the application is ready to receive requests.");
            }
        }

        private bool IsNotServerLifeCheck(MockTracerAgent.Span span)
        {
            var url = SpanExpectation.GetTag(span, Tags.HttpUrl);
            if (url == null)
            {
                return true;
            }

            return !url.Contains("alive-check") && !url.Contains("shutdown");
        }

        private async Task<HttpStatusCode> SubmitRequest(string path)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"http://localhost:{HttpPort}{path}");
            string responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[http] {response.StatusCode} {responseText}");
            return response.StatusCode;
        }
    }
}
