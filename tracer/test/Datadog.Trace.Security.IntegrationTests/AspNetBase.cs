// <copyright file="AspNetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetBase : TestHelper
    {
        private readonly HttpClient _httpClient;
        private readonly string _shutdownPath;
        private int _httpPort;
        private Process _process;
        private MockTracerAgent _agent;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null)
            : base(sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
        {
            _httpClient = new HttpClient();
            _shutdownPath = shutdownPath;

            // adding these header so we can later assert it was collect properly
            _httpClient.DefaultRequestHeaders.Add("X-FORWARDED", "86.242.244.246");
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Mistake Not...");
        }

        public async Task<MockTracerAgent> RunOnSelfHosted(bool enableSecurity, bool enableBlocking)
        {
            if (_agent == null)
            {
                var agentPort = TcpPortProvider.GetOpenPort();
                _httpPort = TcpPortProvider.GetOpenPort();

                _agent = new MockTracerAgent(agentPort);
            }

            await StartSample(_agent.Port, arguments: null, aspNetCorePort: _httpPort, enableSecurity: enableSecurity, enableBlocking: enableBlocking);
            return _agent;
        }

        public void Dispose()
        {
            var request = WebRequest.CreateHttp($"http://localhost:{_httpPort}{_shutdownPath}");
            request.GetResponse().Close();

            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        if (!_process.WaitForExit(5000))
                        {
                            _process.Kill();
                        }
                    }
                }
                catch
                {
                }

                _process.Dispose();
            }

            _httpClient?.Dispose();
            _agent?.Dispose();
        }

        public async Task TestBlockedRequestAsync(MockTracerAgent agent, bool enableSecurity, HttpStatusCode expectedStatusCode, int expectedSpans, IEnumerable<Action<MockTracerAgent.Span>> assertOnSpans)
        {
            Func<Task<(HttpStatusCode StatusCode, string ResponseText)>> attack = () => SubmitRequest("/Health/?arg=[$slice]");
            var resultRequests = await Task.WhenAll(attack(), attack(), attack(), attack(), attack());
            agent.SpanFilters.Add(s => s.Tags.ContainsKey("http.url") && s.Tags["http.url"].IndexOf("Health", StringComparison.InvariantCultureIgnoreCase) > 0);
            var spans = agent.WaitForSpans(expectedSpans);
            Assert.Equal(expectedSpans, spans.Count());

            var expectedAppSecEvents = enableSecurity ? 5 : 0;
            var actualAppSecEvents = 0;

            var spanIds = spans.Select(s => s.SpanId);

            foreach (var span in spans)
            {
                foreach (var assert in assertOnSpans)
                {
                    assert(span);
                }

                // not all tags will have security events, only the route ones
                var gotTag = span.Tags.TryGetValue(Tags.AppSecJson, out var json);
                if (gotTag)
                {
                    actualAppSecEvents++;

                    var jsonObj = JsonConvert.DeserializeObject<AppSecJson>(json);

                    var item = jsonObj.Triggers.FirstOrDefault();
                    Assert.NotNull(item);

                    var attackEvent = item;
                    var shouldBlock = expectedStatusCode == HttpStatusCode.Forbidden;
                    Assert.Equal("Finds basic MongoDB SQL injection attempts", attackEvent.Rule.Name);

                    // presences of json tag implies span should carry other security tags
                    var securityTags = new Dictionary<string, string>()
                    {
                        { "appsec.event", "true" },
                        { "_dd.origin", "appsec" },
                        { "http.useragent", "Mistake Not..." },
                        // { "network.client.ip", "127.0.0.1" },
                        { "actor.ip", "86.242.244.246" },
                        { "http.request.headers.host", $"localhost:{_httpPort}" },
                        { "http.request.headers.x-forwarded", "86.242.244.246" },
                        { "http.request.headers.user-agent", "Mistake Not..." },
                        // { "http.response.headers.content-type", "text/plain; charset=utf-8" },
                    };
                    foreach (var kvp in securityTags)
                    {
                        Assert.True(span.Tags.TryGetValue(kvp.Key, out var tagValue), $"The tag {kvp.Key} was not found");
                        Assert.Equal(kvp.Value, tagValue);
                    }
                }
            }

            // asserts on request status code
            Assert.All(resultRequests, r => Assert.Equal(r.StatusCode, expectedStatusCode));

            // asserts on the security events
            Assert.Equal(expectedAppSecEvents, actualAppSecEvents);
        }

        protected void SetHttpPort(int httpPort)
        {
            _httpPort = httpPort;
        }

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path)
        {
            var url = $"http://localhost:{_httpPort}{path}";
            var response = await _httpClient.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, responseText);
        }

        private async Task StartSample(int traceAgentPort, string arguments, int? aspNetCorePort = null, string packageVersion = "", int? statsdPort = null, string framework = "", string path = "/Home", bool enableSecurity = true, bool enableBlocking = true)
        {
            var sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework);
            // get path to sample app that the profiler will attach to
            const int mstimeout = 5000;
            var message = "Now listening on:";

            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // EnvironmentHelper.DebugModeEnabled = true;

            Output.WriteLine($"Starting Application: {sampleAppPath}");
            var executable = EnvironmentHelper.IsCoreClr() ? EnvironmentHelper.GetSampleExecutionSource() : sampleAppPath;
            var args = EnvironmentHelper.IsCoreClr() ? $"{sampleAppPath} {arguments ?? string.Empty}" : arguments;

            _process = ProfilerHelper.StartProcessWithProfiler(
                executable,
                EnvironmentHelper,
                args,
                traceAgentPort: traceAgentPort,
                statsdPort: statsdPort,
                aspNetCorePort: aspNetCorePort.GetValueOrDefault(5000),
                enableSecurity: enableSecurity,
                enableBlocking: enableBlocking,
                callTargetEnabled: true);

            // then wait server ready
            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);
            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    if (args.Data.Contains(message))
                    {
                        wh.Set();
                    }

                    Output.WriteLine($"[webserver][stdout] {args.Data}");
                }
            };
            _process.BeginOutputReadLine();

            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Output.WriteLine($"[webserver][stderr] {args.Data}");
                }
            };

            _process.BeginErrorReadLine();

            wh.WaitOne(mstimeout);

            var maxMillisecondsToWait = 15_000;
            var intervalMilliseconds = 500;
            var intervals = maxMillisecondsToWait / intervalMilliseconds;
            var serverReady = false;
            var responseText = string.Empty;

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                HttpStatusCode statusCode = default;

                try
                {
                    (statusCode, responseText) = await SubmitRequest(path);
                }
                catch (Exception ex)
                {
                    Output.WriteLine("SubmitRequest failed during warmup with error " + ex);
                }

                serverReady = statusCode == HttpStatusCode.OK;
                if (!serverReady)
                {
                    Output.WriteLine(responseText);
                }

                if (serverReady)
                {
                    break;
                }

                Thread.Sleep(intervalMilliseconds);
            }

            if (!serverReady)
            {
                _process.Kill();
                throw new Exception($"Couldn't verify the application is ready to receive requests: {responseText}");
            }
        }
    }
}
