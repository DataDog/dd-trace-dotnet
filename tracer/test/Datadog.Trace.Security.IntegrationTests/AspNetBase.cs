// <copyright file="AspNetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using VerifyTests;
using VerifyXunit;
using Xunit.Abstractions;
using static System.Net.WebRequestMethods;

namespace Datadog.Trace.Security.IntegrationTests
{
    [UsesVerify]
    public class AspNetBase : TestHelper
    {
        protected const string DefaultAttackUrl = "/Health/?arg=[$slice]";
        protected const string DefaultRuleFile = "ruleset.3.0.json";
        protected const string MainIp = "86.242.244.246";
        protected const string Prefix = "Security.";
        private const string XffHeader = "X-FORWARDED-FOR";
        private static readonly Regex AppSecWafDuration = new(@"_dd.appsec.waf.duration: \d+\.0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecWafDurationWithBindings = new(@"_dd.appsec.waf.duration_ext: \d+\.0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecWafVersion = new(@"\s*_dd.appsec.waf.version: \d+.\d+.\d+(\S*)?,", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecWafRulesVersion = new(@"\s*_dd.appsec.event_rules.version: \d+.\d+.\d+(\S*)?,", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecEventRulesLoaded = new(@"\s*_dd.appsec.event_rules.loaded: \d+\.0,?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecErrorCount = new(@"\s*_dd.appsec.event_rules.error_count: 0.0,?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly string _testName;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly string _shutdownPath;
        private readonly JsonSerializerSettings _jsonSerializerSettingsOrderProperty;
        private int _httpPort;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null, bool changeDefaults = false)
            : base(!changeDefaults ? Prefix + sampleName : sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
        {
            _testName = (!changeDefaults ? Prefix : string.Empty) + (testName ?? sampleName);
            _httpClient = new HttpClient();
            _testName = Prefix + (testName ?? sampleName);

            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler();
            handler.CookieContainer = _cookieContainer;
            _httpClient = new HttpClient(handler);
            _shutdownPath = shutdownPath;

            // adding these header so we can later assert it was collected properly
            _httpClient.DefaultRequestHeaders.Add(XffHeader, MainIp);
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Mistake Not...");

#if NETCOREAPP2_1
            // Keep-alive is causing some weird failures on aspnetcore 2.1
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
#endif

            _jsonSerializerSettingsOrderProperty = new JsonSerializerSettings { ContractResolver = new OrderedContractResolver() };
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_APPSEC_WAF_TIMEOUT", 10_000_000.ToString());
        }

        protected bool IncludeAllHttpSpans { get; set; } = false;

        public override void Dispose()
        {
            base.Dispose();
            _httpClient?.Dispose();
        }

        public void AddCookies(Dictionary<string, string> cookiesValues)
        {
            foreach (var cookie in cookiesValues)
            {
                _cookieContainer.Add(new Cookie(cookie.Key, cookie.Value, string.Empty, "localhost"));
            }
        }

        public async Task TestAppSecRequestWithVerifyAsync(MockTracerAgent agent, string url, string body, int expectedSpans, int spansPerRequest, VerifySettings settings, string contentType = null, bool testInit = false, string userAgent = null, string methodNameOverride = null)
        {
            var spans = await SendRequestsAsync(agent, url, body, expectedSpans, expectedSpans * spansPerRequest, string.Empty, contentType, userAgent);
            await VerifySpans(spans, settings, testInit, methodNameOverride);
        }

        public async Task VerifySpans(IImmutableList<MockSpan> spans, VerifySettings settings, bool testInit = false, string methodNameOverride = null, string testName = null)
        {
            settings.ModifySerialization(
                serializationSettings =>
                {
                    serializationSettings.MemberConverter<MockSpan, Dictionary<string, string>>(
                        sp => sp.Tags,
                        (target, value) =>
                        {
                            if (target.Tags.TryGetValue(Tags.AppSecJson, out var appsecJson))
                            {
                                var appSecJsonObj = JsonConvert.DeserializeObject<AppSecJson>(appsecJson);
                                var orderedAppSecJson = JsonConvert.SerializeObject(appSecJsonObj, _jsonSerializerSettingsOrderProperty);
                                target.Tags[Tags.AppSecJson] = orderedAppSecJson;
                            }

                            return VerifyHelper.ScrubStackTraceForErrors(target, target.Tags);
                        });
                });
            settings.AddRegexScrubber(AppSecWafDuration, "_dd.appsec.waf.duration: 0.0");
            settings.AddRegexScrubber(AppSecWafDurationWithBindings, "_dd.appsec.waf.duration_ext: 0.0");
            if (!testInit)
            {
                settings.AddRegexScrubber(AppSecWafVersion, string.Empty);
                settings.AddRegexScrubber(AppSecWafRulesVersion, string.Empty);
                settings.AddRegexScrubber(AppSecErrorCount, string.Empty);
                settings.AddRegexScrubber(AppSecEventRulesLoaded, string.Empty);
            }

            var appsecSpans = spans.Where(s => s.Tags.ContainsKey("_dd.appsec.json"));
            if (appsecSpans.Any())
            {
                appsecSpans.Should().OnlyContain(s => s.Metrics["_dd.appsec.waf.duration"] < s.Metrics["_dd.appsec.waf.duration_ext"]);
            }

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName(methodNameOverride ?? "_")
                          .UseTypeName(testName ?? GetTestName());
        }

        protected void SetClientIp(string ip)
        {
            _httpClient.DefaultRequestHeaders.Remove(XffHeader);
            _httpClient.DefaultRequestHeaders.Add(XffHeader, ip);
        }

        protected async Task TestRateLimiter(bool enableSecurity, string url, MockTracerAgent agent, int appsecTraceRateLimit, int totalRequests, int spansPerRequest)
        {
            var errorMargin = 0.15;
            int warmupRequests = 29;
            await SendRequestsAsync(agent, url, null, warmupRequests, warmupRequests * spansPerRequest, string.Empty, "Warmup");

            var iterations = 20;

            var testStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                var start = DateTime.Now;
                var nextBatch = start.AddSeconds(1);

                await SendRequestsAsyncNoWaitForSpans(url, null, totalRequests, string.Empty);

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

            var allSpansReceived = WaitForSpans(agent, iterations * totalRequests * spansPerRequest, "Overall wait", testStart, url);

            var groupedSpans = allSpansReceived.GroupBy(
                s =>
                {
                    var time = new DateTimeOffset((s.Start / TimeConstants.NanoSecondsPerTick) + TimeConstants.UnixEpochInTicks, TimeSpan.Zero);
                    return time.Second;
                });

            var spansWithUserKeep = allSpansReceived.Where(
                s =>
                {
                    s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                    s.Metrics.TryGetValue("_sampling_priority_v1", out var samplingPriority);
                    return ((enableSecurity && appsecevent == "true") || !enableSecurity) && samplingPriority == 2.0;
                });

            var spansWithoutUserKeep = allSpansReceived.Where(
                s =>
                {
                    s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                    return ((enableSecurity && appsecevent == "true") || !enableSecurity) && (!s.Metrics.ContainsKey("_sampling_priority_v1") || s.Metrics["_sampling_priority_v1"] != 2.0);
                });
            var itemsCount = allSpansReceived.Count();
            var appsecItemsCount = allSpansReceived.Where(
                                                        s =>
                                                        {
                                                            s.Tags.TryGetValue(Tags.AppSecEvent, out var appsecevent);
                                                            return appsecevent == "true";
                                                        })
                                                   .Count();
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

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path, string body, string contentType, string userAgent)
        {
            var values = _httpClient.DefaultRequestHeaders.GetValues("user-agent");

            if (!string.IsNullOrEmpty(userAgent) && values.All(c => string.Compare(c, userAgent, StringComparison.Ordinal) != 0))
            {
                _httpClient.DefaultRequestHeaders.Add("user-agent", userAgent);
            }

            try
            {
                var url = $"http://localhost:{_httpPort}{path}";

                var response =
                    body == null ? await _httpClient.GetAsync(url) : await _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, contentType ?? "application/json"));
                var responseText = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, responseText);
            }
            catch (HttpRequestException ex)
            {
                return (HttpStatusCode.BadRequest, ex.ToString());
            }
        }

        protected virtual string GetTestName() => _testName;

        protected async Task<IImmutableList<MockSpan>> SendRequestsAsync(MockTracerAgent agent, string url, string body, int numberOfAttacks, int expectedSpans, string phase, string contentType = null, string userAgent = null)
        {
            var minDateTime = DateTime.UtcNow; // when ran sequentially, we get the spans from the previous tests!
            await SendRequestsAsyncNoWaitForSpans(url, body, numberOfAttacks, contentType, userAgent);

            return WaitForSpans(agent, expectedSpans, phase, minDateTime, url);
        }

        protected Task<IImmutableList<MockSpan>> SendRequestsAsync(MockTracerAgent agent, params string[] urls)
        {
            return SendRequestsAsync(agent, 1, urls);
        }

        protected async Task<IImmutableList<MockSpan>> SendRequestsAsync(MockTracerAgent agent, int expectedSpansPerRequest, params string[] urls)
        {
            var spans = new List<MockSpan>();
            foreach (var url in urls)
            {
                spans.AddRange(await SendRequestsAsync(agent, url, null, 1, expectedSpansPerRequest, string.Empty));
            }

            return spans.ToImmutableList();
        }

        private async Task SendRequestsAsyncNoWaitForSpans(string url, string body, int numberOfAttacks, string contentType = null, string userAgent = null)
        {
            for (var x = 0; x < numberOfAttacks; x++)
            {
                await SubmitRequest(url, body, contentType, userAgent);
            }
        }

        private IImmutableList<MockSpan> WaitForSpans(MockTracerAgent agent, int expectedSpans, string phase, DateTime minDateTime, string url)
        {
            agent.SpanFilters.Clear();

            if (!IncludeAllHttpSpans)
            {
                agent.SpanFilters.Add(s => s.Tags.ContainsKey("http.url") && s.Tags["http.url"].IndexOf(url, StringComparison.InvariantCultureIgnoreCase) > -1);
            }

            var spans = agent.WaitForSpans(expectedSpans, minDateTime: minDateTime);
            if (spans.Count != expectedSpans)
            {
                Output?.WriteLine($"spans.Count: {spans.Count} != expectedSpans: {expectedSpans}, this is phase: {phase}");
            }

            return spans;
        }

        internal class AppSecJson
        {
            [JsonProperty("triggers")]
            public WafMatch[] Triggers { get; set; }
        }
    }
}
