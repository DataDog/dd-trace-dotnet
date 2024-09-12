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
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using VerifyTests;
using VerifyXunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    [UsesVerify]
    public class AspNetBase : TestHelper
    {
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1202 // Elements should be ordered by access
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
        private static readonly Regex AppSecRaspWafDuration = new(@"_dd.appsec.rasp.duration: \d+\.0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecRaspWafDurationWithBindings = new(@"_dd.appsec.rasp.duration_ext: \d+\.0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AppSecSpanIdRegex = (new Regex("\"span_id\":\\d+"));
        private static readonly Type MetaStructHelperType = Type.GetType("Datadog.Trace.AppSec.Rasp.MetaStructHelper, Datadog.Trace");
        private static readonly MethodInfo MetaStructByteArrayToObject = MetaStructHelperType.GetMethod("ByteArrayToObject", BindingFlags.Public | BindingFlags.Static);
        protected string _testName;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly string _shutdownPath;
        private readonly JsonSerializerSettings _jsonSerializerSettingsOrderProperty;
        private readonly bool _clearMetaStruct;
        private int _httpPort;
#pragma warning restore SA1202 // Elements should be ordered by access
#pragma warning restore SA1401 // Fields should be private

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null, bool clearMetaStruct = false)
            : base(Prefix + sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
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
            _jsonSerializerSettingsOrderProperty = new JsonSerializerSettings { ContractResolver = new OrderedContractResolver() };

            _clearMetaStruct = clearMetaStruct;
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

        public async Task TestAppSecRequestWithVerifyAsync(MockTracerAgent agent, string url, string body, int expectedSpans, int spansPerRequest, VerifySettings settings, string contentType = null, bool testInit = false, string userAgent = null, string methodNameOverride = null, string fileNameOverride = null)
        {
            var spans = await SendRequestsAsync(agent, url, body, expectedSpans, expectedSpans * spansPerRequest, string.Empty, contentType, userAgent);
            await VerifySpans(spans, settings, testInit, methodNameOverride, fileNameOverride: fileNameOverride);
        }

        public async Task VerifySpans(IImmutableList<MockSpan> spans, VerifySettings settings, bool testInit = false, string methodNameOverride = null, string testName = null, bool forceMetaStruct = false, string fileNameOverride = null)
        {
            settings.ModifySerialization(
                serializationSettings =>
                {
                    serializationSettings.MemberConverter<MockSpan, Dictionary<string, string>>(
                        sp => sp.Tags,
                        (target, value) =>
                        {
                            if (target.Tags.Any(t => t.Key.StartsWith("_dd.appsec.s.re")))
                            {
                                var apisecurityTags = target.Tags.Where(t => t.Key.StartsWith("_dd.appsec.s.re")).ToList();

                                foreach (var tag in apisecurityTags)
                                {
                                    var bytes = System.Convert.FromBase64String(tag.Value);
                                    using var memoryStream = new MemoryStream(bytes);
                                    using var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                                    gZipStream.Flush();
                                    var t = JsonSerializer.Create(_jsonSerializerSettingsOrderProperty);
                                    using var textReader = new JsonTextReader(new StreamReader(gZipStream));
                                    // this will work until children have more complex properties with more order
                                    var result = t.Deserialize<JArray>(textReader);

                                    SortJToken(result);
                                    target.Tags[tag.Key] = JsonConvert.SerializeObject(result);
                                }
                            }

                            if (target.Tags.TryGetValue(Tags.AppSecJson, out var appsecJson))
                            {
                                var appSecJsonObj = JsonConvert.DeserializeObject<AppSecJson>(appsecJson);
                                var orderedAppSecJson = JsonConvert.SerializeObject(appSecJsonObj, _jsonSerializerSettingsOrderProperty);
                                target.Tags[Tags.AppSecJson] = orderedAppSecJson;
                            }

                            if (target.MetaStruct != null)
                            {
                                // We want to retrieve the appsec event data from the meta struct to validate it in snapshots
                                // But that's hard to debug if we only see the binary data
                                // So move the meta struct appsec data to a fake tag to validate it in snapshots
                                if (target.MetaStruct.TryGetValue("appsec", out var appsec))
                                {
                                    var appSecMetaStruct = MetaStructByteArrayToObject.Invoke(null, [appsec]);
                                    var json = JsonConvert.SerializeObject(appSecMetaStruct);
                                    var obj = JsonConvert.DeserializeObject<AppSecJson>(json);
                                    var orderedJson = JsonConvert.SerializeObject(obj, _jsonSerializerSettingsOrderProperty);
                                    target.Tags[Tags.AppSecJson] = orderedJson;

                                    // Let the snapshot know that the data comes from the meta struct
                                    if (forceMetaStruct)
                                    {
                                        target.Tags[Tags.AppSecJson + ".metastruct.test"] = "true";
                                    }
                                }

                                if (_clearMetaStruct)
                                {
                                    target.MetaStruct = null;
                                }
                                else
                                {
                                    // Remove all data from meta structs keys, no need to get the binary data for other keys
                                    foreach (var key in target.MetaStruct.Keys.ToList())
                                    {
                                        target.MetaStruct[key] = [];
                                    }
                                }
                            }

                            return VerifyHelper.ScrubStringTags(target, target.Tags);
                        });
                });
            settings.AddRegexScrubber(AppSecWafDuration, "_dd.appsec.waf.duration: 0.0");
            settings.AddRegexScrubber(AppSecWafDurationWithBindings, "_dd.appsec.waf.duration_ext: 0.0");
            settings.AddRegexScrubber(AppSecRaspWafDuration, "_dd.appsec.rasp.duration: 0.0");
            settings.AddRegexScrubber(AppSecRaspWafDurationWithBindings, "_dd.appsec.rasp.duration_ext: 0.0");
            settings.AddRegexScrubber(AppSecSpanIdRegex, "\"span_id\": XXX");
            if (!testInit)
            {
                settings.AddRegexScrubber(AppSecWafVersion, string.Empty);
                settings.AddRegexScrubber(AppSecWafRulesVersion, string.Empty);
                settings.AddRegexScrubber(AppSecErrorCount, string.Empty);
                settings.AddRegexScrubber(AppSecEventRulesLoaded, string.Empty);
            }

            var appsecSpans = spans.Where(s => s.Tags.ContainsKey("_dd.appsec.json") || (s.MetaStruct != null && s.MetaStruct.ContainsKey("appsec")));
            if (appsecSpans.Any())
            {
                appsecSpans.Should().OnlyContain(s => s.Metrics["_dd.appsec.waf.duration"] < s.Metrics["_dd.appsec.waf.duration_ext"]
                || s.Metrics["_dd.appsec.rasp.duration"] < s.Metrics["_dd.appsec.rasp.duration_ext"]);
            }

            if (string.IsNullOrEmpty(fileNameOverride))
            {
                // Overriding the type name here as we have multiple test classes in the file
                // Ensures that we get nice file nesting in Solution Explorer
                await Verifier.Verify(spans, settings)
                              .UseMethodName(methodNameOverride ?? "_")
                              .UseTypeName(testName ?? GetTestName());
            }
            else
            {
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(fileNameOverride)
                                  .DisableRequireUniquePrefix();
            }
        }

        public void StacksMetaStructScrubbing(MockSpan target)
        {
            var key = "_dd.stack";
            if (target.MetaStruct is not null && target.MetaStruct.TryGetValue(key, out var appsec))
            {
                var metaStruct = MetaStructByteArrayToObject.Invoke(null, [appsec]);
                var json = JsonConvert.SerializeObject(metaStruct, Formatting.Indented);
                target.Tags[key] = json;
            }
        }

        protected void SetClientIp(string ip)
        {
            _httpClient.DefaultRequestHeaders.Remove(XffHeader);
            _httpClient.DefaultRequestHeaders.Add(XffHeader, ip);
        }

        protected string MetaStructToJson(byte[] data)
        {
            var metaStruct = MetaStructByteArrayToObject.Invoke(null, [data]);
            var json = JsonConvert.SerializeObject(metaStruct, Formatting.Indented);
            return json;
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
