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
using Xunit;
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

        public async Task<MockTracerAgent> RunOnSelfHosted(bool enableSecurity, bool enableBlocking, string externalRulesFile = null, int? traceRateLimit = null)
        {
            if (_agent == null)
            {
                var agentPort = TcpPortProvider.GetOpenPort();
                _httpPort = TcpPortProvider.GetOpenPort();

                _agent = new MockTracerAgent(agentPort);
            }

            await StartSample(
                _agent,
                arguments: null,
                aspNetCorePort: _httpPort,
                enableSecurity: enableSecurity,
                enableBlocking: enableBlocking,
                externalRulesFile: externalRulesFile,
                traceRateLimit: traceRateLimit);

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

        public async Task TestBlockedRequestWithVerifyAsync(MockTracerAgent agent, string url, int expectedSpans, VerifySettings settings, int attackNumber = 5)
        {
            var spans = await SendRequestsAsync(agent, url, expectedSpans, attackNumber);

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

        protected async Task<IImmutableList<MockSpan>> SendRequestsAsync(MockTracerAgent agent, string url, int expectedSpans, int attackNumber, bool parallel = false)
        {
            var minDateTime = DateTime.UtcNow; // when ran sequentially, we get the spans from the previous tests!
            var attacks = new ConcurrentBag<Task<(HttpStatusCode, string)>>();
            if (parallel)
            {
                Parallel.For(0, attackNumber, _ => attacks.Add(SubmitRequest(url)));
            }
            else
            {
                for (int i = 0; i < attackNumber; i++)
                {
                    attacks.Add(SubmitRequest(url));
                }
            }

            var resultRequests = await Task.WhenAll(attacks);
            agent.SpanFilters.Add(s => s.Tags.ContainsKey("http.url") && s.Tags["http.url"].IndexOf("Health", StringComparison.InvariantCultureIgnoreCase) > 0);
            var spans = agent.WaitForSpans(expectedSpans, minDateTime: minDateTime);
            Assert.Equal(expectedSpans, spans.Count);
            return spans;
        }

        protected async Task TestRateLimiter(bool enableSecurity, string url, MockTracerAgent agent, int appsecTraceRateLimit, int totalRequests, int totalExpectedSpans, bool parallel = true, int expectedSpansonWarmupFactor = 1)
        {
            var errorMargin = 0.10;
            int expectedSpansonWarmup = 5 * expectedSpansonWarmupFactor;
            await SendRequestsAsync(agent, url, expectedSpans: expectedSpansonWarmup, 5);
            var spansReceived = await SendRequestsAsync(agent, url, expectedSpans: totalExpectedSpans, totalRequests, parallel);
            var groupedSpans = spansReceived.GroupBy(s =>
            {
                var time = new DateTimeOffset((s.Start / TimeConstants.NanoSecondsPerTick) + TimeConstants.UnixEpochInTicks, TimeSpan.Zero);
                return time.Second;
            });
            var firstSpan = groupedSpans.OrderBy(g => g.Key).FirstOrDefault();
            foreach (var itemsPerSecond in groupedSpans)
            {
                var spansWithUserKeep = itemsPerSecond.Where(s =>
                {
                    s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                    s.Metrics.TryGetValue("_sampling_priority_v1", out var samplingPriority);
                    return ((enableSecurity && appsecevent == "true") || !enableSecurity) && samplingPriority == 2.0;
                });

                var spansWithoutUserKeep = itemsPerSecond.Where(s =>
                {
                    s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                    return ((enableSecurity && appsecevent == "true") || !enableSecurity) && (!s.Metrics.ContainsKey("_sampling_priority_v1") || s.Metrics["_sampling_priority_v1"] != 2.0);
                });
                var itemsCount = itemsPerSecond.Count();
                var appsecItemsCount = itemsPerSecond.Where(s =>
                {
                    s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                    return appsecevent != "true";
                }).Count();
                if (enableSecurity)
                {
                    var message = "approximate because of parallel requests";
                    if (appsecItemsCount >= appsecTraceRateLimit)
                    {
                        var excess = Math.Abs(itemsCount - appsecTraceRateLimit);
                        spansWithUserKeep.Count().Should().BeCloseTo(appsecTraceRateLimit, (uint)(appsecTraceRateLimit * errorMargin), message);
                        spansWithoutUserKeep.Count().Should().BeCloseTo(excess, (uint)(appsecTraceRateLimit * errorMargin), message);
                        if (excess > 0)
                        {
                            spansWithoutUserKeep.Should().Contain(s => s.Metrics.ContainsKey("_dd.appsec.rate_limit.dropped_traces"));
                        }
                    }
                    else
                    {
                        spansWithUserKeep.Count().Should().BeLessOrEqualTo(appsecTraceRateLimit);
                        spansWithoutUserKeep.Count().Should().Be(0);
                    }
                }
                else
                {
                    spansWithoutUserKeep.Count().Should().Be(itemsCount);
                    spansWithoutUserKeep.Should().NotContain(s => s.Metrics.ContainsKey("_dd.appsec.rate_limit.dropped_traces"));
                }
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

        private async Task StartSample(
            MockTracerAgent agent,
            string arguments,
            int? aspNetCorePort = null,
            string packageVersion = "",
            string framework = "",
            string path = "/Home",
            bool enableSecurity = true,
            bool enableBlocking = true,
            string externalRulesFile = null,
            int? traceRateLimit = null)
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
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_TRACE_RATE_LIMIT", traceRateLimit?.ToString());

            _process = ProfilerHelper.StartProcessWithProfiler(
                executable,
                EnvironmentHelper,
                agent,
                args,
                aspNetCorePort: aspNetCorePort.GetValueOrDefault(5000),
                enableSecurity: enableSecurity,
                enableBlocking: enableBlocking,
                externalRulesFile: externalRulesFile);

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
