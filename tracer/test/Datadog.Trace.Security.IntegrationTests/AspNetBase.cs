// <copyright file="AspNetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using FluentAssertions;
using VerifyTests;
using VerifyXunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    [UsesVerify]
    public class AspNetBase : TestHelper
    {
        protected const string DefaultAttackUrl = "/Health/?arg=[$slice]";
        private readonly string _testName;
        private readonly HttpClient _httpClient;
        private readonly string _shutdownPath;
        private readonly JsonSerializerSettings _jsonSerializerSettingsOrderProperty;
        private int _httpPort;
        private Process _process;
        private MockTracerAgent _agent;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null)
            : base(sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
        {
            _testName = "Security." + (testName ?? sampleName);
            _httpClient = new HttpClient();
            _shutdownPath = shutdownPath;

            // adding these header so we can later assert it was collect properly
            _httpClient.DefaultRequestHeaders.Add("X-FORWARDED", "86.242.244.246");
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Mistake Not...");
            _jsonSerializerSettingsOrderProperty = new JsonSerializerSettings
            {
                ContractResolver = new OrderedContractResolver()
            };
        }

        public Task<MockTracerAgent> RunOnSelfHosted(bool enableSecurity, string externalRulesFile = null, int? traceRateLimit = null)
        {
            if (_agent == null)
            {
                var agentPort = TcpPortProvider.GetOpenPort();
                _agent = new MockTracerAgent(agentPort);
            }

            StartSample(
                _agent,
                arguments: null,
                enableSecurity: enableSecurity,
                externalRulesFile: externalRulesFile,
                traceRateLimit: traceRateLimit);

            return Task.FromResult(_agent);
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

        public async Task TestBlockedRequestWithVerifyAsync(MockTracerAgent agent, string url, int expectedSpans, int spansPerRequest, VerifySettings settings)
        {
            var spans = await SendRequestsAsync(agent, url, expectedSpans, expectedSpans * spansPerRequest, string.Empty);

            settings.ModifySerialization(serializationSettings => serializationSettings.MemberConverter<MockSpan, Dictionary<string, string>>(sp => sp.Tags, (target, value) =>
            {
                if (target.Tags.TryGetValue(Tags.AppSecJson, out var appsecJson))
                {
                    var appSecJsonObj = JsonConvert.DeserializeObject<AppSecJson>(appsecJson);
                    var orderedAppSecJson = JsonConvert.SerializeObject(appSecJsonObj, _jsonSerializerSettingsOrderProperty);
                    target.Tags[Tags.AppSecJson] = orderedAppSecJson;
                }

                return target.Tags;
            }));
            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(GetTestName());
        }

        protected async Task TestRateLimiter(bool enableSecurity, string url, MockTracerAgent agent, int appsecTraceRateLimit, int totalRequests, int spansPerRequest)
        {
            var errorMargin = 0.15;
            int warmupRequests = 29;
            await SendRequestsAsync(agent, url, warmupRequests, warmupRequests * spansPerRequest, "Warmup");

            var iterations = 20;

            var testStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                var start = DateTime.Now;
                var nextBatch = start.AddSeconds(1);

                await SendRequestsAsyncNoWaitForSpans(url, totalRequests);

                var now = DateTime.Now;

                if (now > nextBatch)
                {
                    // attempt to compensate for slow servers by increasing the error margin
                    errorMargin *= 1.2;
                    Console.WriteLine($"Failed to send all requests within a second now: {now:hh:mm:ss.fff}, nextBatch:{nextBatch:hh:mm:ss.fff}, error margin now: {errorMargin}");
                }
                else
                {
                    await Task.Delay(nextBatch - now);
                }
            }

            var allSpansReceived = WaitForSpans(agent, iterations * totalRequests * spansPerRequest, "Overall wait", testStart);

            var groupedSpans = allSpansReceived.GroupBy(s =>
            {
                var time = new DateTimeOffset((s.Start / TimeConstants.NanoSecondsPerTick) + TimeConstants.UnixEpochInTicks, TimeSpan.Zero);
                return time.Second;
            });

            var spansWithUserKeep = allSpansReceived.Where(s =>
            {
                s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                s.Metrics.TryGetValue("_sampling_priority_v1", out var samplingPriority);
                return ((enableSecurity && appsecevent == "true") || !enableSecurity) && samplingPriority == 2.0;
            });

            var spansWithoutUserKeep = allSpansReceived.Where(s =>
            {
                s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                return ((enableSecurity && appsecevent == "true") || !enableSecurity) && (!s.Metrics.ContainsKey("_sampling_priority_v1") || s.Metrics["_sampling_priority_v1"] != 2.0);
            });
            var itemsCount = allSpansReceived.Count();
            var appsecItemsCount = allSpansReceived.Where(s =>
            {
                s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                return appsecevent == "true";
            }).Count();
            if (enableSecurity)
            {
                var message = "approximate because of parallel requests";
                var rateLimitOverPeriod = appsecTraceRateLimit * iterations;
                if (appsecItemsCount >= rateLimitOverPeriod)
                {
                    var excess = appsecItemsCount - rateLimitOverPeriod;
                    var spansWithUserKeepCount = spansWithUserKeep.Count();
                    var spansWithoutUserKeepCount = spansWithoutUserKeep.Count();

                    Console.WriteLine($"spansWithUserKeepCount: {rateLimitOverPeriod}, appsecTraceRateLimit: {rateLimitOverPeriod}");
                    Console.WriteLine($"spansWithoutUserKeepCount: {spansWithoutUserKeepCount}, excess: {excess}");

                    spansWithUserKeepCount.Should().BeCloseTo(rateLimitOverPeriod, (uint)(rateLimitOverPeriod * errorMargin), message);
                    spansWithoutUserKeepCount.Should().BeCloseTo(excess, (uint)(rateLimitOverPeriod * errorMargin), message);
                }
                else
                {
                    var spansWithUserKeepCount = spansWithUserKeep.Count();
                    var spansWithoutUserKeepCount = spansWithoutUserKeep.Count();
                    Console.WriteLine($"spansWithUserKeep: {spansWithUserKeepCount}, rateLimitOverPeriod: {rateLimitOverPeriod}");
                    Console.WriteLine($"spansWithoutUserKeep: {spansWithoutUserKeepCount}, excess: 0");

                    spansWithUserKeepCount.Should().BeLessThan(rateLimitOverPeriod + (int)(rateLimitOverPeriod * errorMargin));
                    spansWithoutUserKeepCount.Should().BeCloseTo(0, (uint)(rateLimitOverPeriod * errorMargin), message);
                }
            }
            else
            {
                spansWithoutUserKeep.Count().Should().Be(itemsCount);
            }
        }

        protected void SetHttpPort(int httpPort) => _httpPort = httpPort;

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path)
        {
            var url = $"http://localhost:{_httpPort}{path}";
            var response = await _httpClient.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, responseText);
        }

        protected virtual string GetTestName() => _testName;

        private async Task<IImmutableList<MockSpan>> SendRequestsAsync(MockTracerAgent agent, string url, int numberOfAttacks, int expectedSpans, string phase)
        {
            var minDateTime = DateTime.UtcNow; // when ran sequentially, we get the spans from the previous tests!
            await SendRequestsAsyncNoWaitForSpans(url, numberOfAttacks);

            return WaitForSpans(agent, expectedSpans, phase, minDateTime);
        }

        private async Task SendRequestsAsyncNoWaitForSpans(string url, int numberOfAttacks)
        {
            var batchSize = 4;
            for (int x = 0; x < numberOfAttacks;)
            {
                var attacks = new ConcurrentBag<Task<(HttpStatusCode, string)>>();
                for (int y = 0; y < batchSize && x < numberOfAttacks;)
                {
                    x++;
                    y++;
                    attacks.Add(SubmitRequest(url));
                }

                await Task.WhenAll(attacks);
            }
        }

        private IImmutableList<MockSpan> WaitForSpans(MockTracerAgent agent, int expectedSpans, string phase, DateTime minDateTime)
        {
            agent.SpanFilters.Add(s => s.Tags.ContainsKey("http.url") && s.Tags["http.url"].IndexOf("Health", StringComparison.InvariantCultureIgnoreCase) > 0);

            var spans = agent.WaitForSpans(expectedSpans, minDateTime: minDateTime);
            spans.Count.Should().Be(expectedSpans, "This is phase: {0}", phase);

            return spans;
        }

        private void StartSample(
            MockTracerAgent agent,
            string arguments,
            string packageVersion = "",
            string framework = "",
            string path = "/Home",
            bool enableSecurity = true,
            string externalRulesFile = null,
            int? traceRateLimit = null)
        {
            var sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework);
            // get path to sample app that the profiler will attach to
            const int mstimeout = 15_000;

            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // EnvironmentHelper.DebugModeEnabled = true;

            Output.WriteLine($"Starting Application: {sampleAppPath}");
            var executable = EnvironmentHelper.IsCoreClr() ? EnvironmentHelper.GetSampleExecutionSource() : sampleAppPath;
            var args = EnvironmentHelper.IsCoreClr() ? $"{sampleAppPath} {arguments ?? string.Empty}" : arguments;
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_TRACE_RATE_LIMIT", traceRateLimit?.ToString());

            int? aspNetCorePort = default;
            _process = ProfilerHelper.StartProcessWithProfiler(
                executable,
                EnvironmentHelper,
                agent,
                args,
                aspNetCorePort: 0,
                enableSecurity: enableSecurity,
                externalRulesFile: externalRulesFile);

            // then wait server ready
            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);
            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    if (args.Data.Contains("Now listening on:"))
                    {
                        var splitIndex = args.Data.LastIndexOf(':');
                        aspNetCorePort = int.Parse(args.Data.Substring(splitIndex + 1));
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
            if (!aspNetCorePort.HasValue)
            {
                _process.Kill();
                throw new Exception("Unable to determine port application is listening on");
            }

            _httpPort = aspNetCorePort.Value;
        }
    }
}
