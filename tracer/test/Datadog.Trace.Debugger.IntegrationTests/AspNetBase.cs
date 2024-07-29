// <copyright file="AspNetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using VerifyTests;
using VerifyXunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests
{
    [UsesVerify]
    public class AspNetBase : TestHelper
    {
        protected const string MainIp = "86.242.244.246";
        protected const string Prefix = "Debugger.";
        private const string XffHeader = "X-FORWARDED-FOR";
        private readonly string _testName;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly string _shutdownPath;
        private int _httpPort;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null)
            : base(Prefix + sampleName, samplesDir ?? "test/test-applications/debugger", outputHelper)
        {
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
        }

        protected bool IncludeAllHttpSpans { get; set; } = false;

        public override void Dispose()
        {
            base.Dispose();
            _httpClient?.Dispose();
        }

        public void AddHeaders(Dictionary<string, string> headersValues)
        {
            foreach (var header in headersValues)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        public void AddCookies(Dictionary<string, string> cookiesValues)
        {
            foreach (var cookie in cookiesValues)
            {
                _cookieContainer.Add(new Cookie(cookie.Key, cookie.Value, string.Empty, "localhost"));
            }
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

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path, string body, string contentType, string userAgent = null, string accept = null, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            var values = _httpClient.DefaultRequestHeaders.GetValues("user-agent");

            if (!string.IsNullOrEmpty(userAgent) && values.All(c => string.Compare(c, userAgent, StringComparison.Ordinal) != 0))
            {
                _httpClient.DefaultRequestHeaders.Add("user-agent", userAgent);
            }

            if (accept != null)
            {
                _httpClient.DefaultRequestHeaders.Add("accept", accept);
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            try
            {
                var url = $"http://localhost:{_httpPort}{path}";

                var response = body == null ? await _httpClient.GetAsync(url) : await _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, contentType ?? "application/json"));
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

        protected IImmutableList<MockSpan> WaitForSpans(MockTracerAgent agent, int expectedSpans, string phase, DateTime minDateTime, string url)
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

        private void SortJToken(JToken result)
        {
            IEnumerable<JToken> res;
            switch (result)
            {
                case JArray jarray:
                    var children = jarray.Children().ToList();
                    res = children.OrderBy(r => r.Path).ToList();
                    if (children.Count > 1)
                    {
                        for (var i = 0; i < children.Count; i++)
                        {
                            children[i].Remove();
                        }

                        foreach (var item in res)
                        {
                            SortJToken(item);
                            jarray.Add(item);
                        }
                    }
                    else
                    {
                        var firstChild = children.First();
                        if (firstChild is not null)
                        {
                            firstChild.Remove();
                            SortJToken(firstChild);
                            jarray.Add(firstChild);
                        }
                    }

                    break;
                case JObject o:
                    res = o.Properties().OrderBy(p => p.Path).ToList();
                    o.RemoveAll();
                    foreach (var item in res)
                    {
                        if (item.First is not null)
                        {
                            SortJToken(item.First);
                        }

                        o.Add(item);
                    }

                    break;
            }
        }

        internal class AppSecJson
        {
            [JsonProperty("triggers")]
            public WafMatch[] Triggers { get; set; }
        }
    }
}
