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
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetBase : TestHelper
    {
        private readonly HttpClient httpClient;
        private readonly string shutdownPath;
        private int httpPort;
        private Process process;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null)
            : base(sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
        {
            httpClient = new HttpClient();
            this.shutdownPath = shutdownPath;
        }

        public async Task<MockTracerAgent> RunOnSelfHosted(bool enableSecurity, bool enableBlocking)
        {
            var agentPort = TcpPortProvider.GetOpenPort();
            httpPort = TcpPortProvider.GetOpenPort();

            var agent = new MockTracerAgent(agentPort);
            await StartSample(agent.Port, arguments: null, aspNetCorePort: httpPort, enableSecurity: enableSecurity, enableBlocking: enableBlocking);
            return agent;
        }

        public void Dispose()
        {
            var request = WebRequest.CreateHttp($"http://localhost:{httpPort}{shutdownPath}");
            request.GetResponse().Close();

            if (process != null && !process.HasExited)
            {
                process.Kill();
                process.Dispose();
            }

            if (httpClient != null)
            {
                httpClient.Dispose();
            }
        }

        public async Task TestBlockedRequestAsync(MockTracerAgent agent, bool enableSecurity, HttpStatusCode expectedStatusCode, int expectedSpans, IEnumerable<Action<MockTracerAgent.Span>> assertOnSpans)
        {
            var mockTracerAgentAppSecWrapper = new MockTracerAgentAppSecWrapper(agent);
            mockTracerAgentAppSecWrapper.SubscribeAppSecEvents();
            Func<Task<(HttpStatusCode StatusCode, string ResponseText)>> attack = () => SubmitRequest("/Health/?arg=[$slice]");
            var resultRequests = await Task.WhenAll(attack(), attack(), attack(), attack(), attack());
            agent.SpanFilters.Add(s => s.Tags["http.url"].IndexOf("Health", StringComparison.InvariantCultureIgnoreCase) > 0);
            var spans = agent.WaitForSpans(expectedSpans);
            Assert.Equal(expectedSpans, spans.Count());
            foreach (var span in spans)
            {
                foreach (var assert in assertOnSpans)
                {
                    assert(span);
                }
            }

            var expectedAppSecEvents = enableSecurity ? 5 : 0;

            // asserts on request status code
            Assert.All(resultRequests, r => Assert.Equal(r.StatusCode, expectedStatusCode));

            var appSecEvents = mockTracerAgentAppSecWrapper.WaitForAppSecEvents(expectedAppSecEvents);

            // asserts on the security events
            Assert.Equal(expectedAppSecEvents, appSecEvents.Count);
            var spanIds = spans.Select(s => s.SpanId);
            var usedIds = new List<ulong>();
            foreach (var item in appSecEvents)
            {
                Assert.IsType<AppSec.EventModel.Attack>(item);
                var attackEvent = (AppSec.EventModel.Attack)item;
                var shouldBlock = expectedStatusCode == HttpStatusCode.Forbidden;
                Assert.Equal(shouldBlock, attackEvent.Blocked);
                var spanId = spanIds.FirstOrDefault(s => s == attackEvent.Context.Span.Id);
                Assert.NotEqual(0m, spanId);
                Assert.DoesNotContain(spanId, usedIds);
                Assert.Equal("nosqli", attackEvent.Rule.Name);
                usedIds.Add(spanId);
            }

            mockTracerAgentAppSecWrapper.UnsubscribeAppSecEvents();
        }

        protected void SetHttpPort(int httpPort)
        {
            this.httpPort = httpPort;
        }

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path)
        {
            var url = $"http://localhost:{httpPort}{path}";
            var response = await httpClient.GetAsync(url);
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

            // get full paths to integration definitions
            var integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            // EnvironmentHelper.DebugModeEnabled = true;

            Output.WriteLine($"Starting Application: {sampleAppPath}");
            var executable = EnvironmentHelper.IsCoreClr() ? EnvironmentHelper.GetSampleExecutionSource() : sampleAppPath;
            var args = EnvironmentHelper.IsCoreClr() ? $"{sampleAppPath} {arguments ?? string.Empty}" : arguments;

            process = ProfilerHelper.StartProcessWithProfiler(
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
            process.OutputDataReceived += (sender, args) =>
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
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Output.WriteLine($"[webserver][stderr] {args.Data}");
                }
            };

            process.BeginErrorReadLine();

            wh.WaitOne(mstimeout);

            var maxMillisecondsToWait = 15_000;
            var intervalMilliseconds = 500;
            var intervals = maxMillisecondsToWait / intervalMilliseconds;
            var serverReady = false;
            var responseText = string.Empty;

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                var response = await SubmitRequest(path);
                responseText = response.ResponseText;
                serverReady = response.StatusCode == HttpStatusCode.OK;
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
                process.Kill();
                throw new Exception($"Couldn't verify the application is ready to receive requests: {responseText}");
            }
        }
    }
}
