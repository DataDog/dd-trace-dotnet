// <copyright file="AspNetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    [UsesVerify]
    public class AspNetBase : TestHelper
    {
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1202 // Elements should be ordered by access
        protected const string DefaultAttackUrl = "/Health/?arg=[$slice]";
        protected const string DefaultRuleFile = "ruleset.3.0.json"; // Test Ruleset without "custom-block" action
        protected const string DefaultFullRuleFile = "ruleset.3.0-full.json"; // Test Ruleset with "custom-block" action
        protected const string MainIp = "86.242.244.246";
        protected const string Prefix = "Security.";
        private const string XffHeader = "X-FORWARDED-FOR";
        private static readonly Type MetaStructHelperType = Type.GetType("Datadog.Trace.AppSec.Rasp.MetaStructHelper, Datadog.Trace");
        private static readonly MethodInfo MetaStructByteArrayToObject = MetaStructHelperType.GetMethod("ByteArrayToObject", BindingFlags.Public | BindingFlags.Static);
        protected string _testName;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly string _shutdownPath;
        private readonly JsonSerializerSettings _jsonSerializerSettingsOrderProperty;
        private int _httpPort;
#pragma warning restore SA1202 // Elements should be ordered by access
#pragma warning restore SA1401 // Fields should be private

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null, bool allowAutoRedirect = true)
            : base(Prefix + sampleName, samplesDir ?? "test/test-applications/security", outputHelper)
        {
            _testName = Prefix + (testName ?? sampleName);
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect };
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

            // without this, the developer exception page intercepts our blocking middleware and doesn't let us write the proper response
            SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
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

        protected void SetHttpPort(int httpPort) => _httpPort = httpPort;

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path, string body, string contentType, string userAgent = null, string accept = null, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            var found = _httpClient.DefaultRequestHeaders.TryGetValues("user-agent", out var values);

            if (!string.IsNullOrEmpty(userAgent) && (!found || values.All(c => string.Compare(c, userAgent, StringComparison.Ordinal) != 0)))
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
                    if (_httpClient.DefaultRequestHeaders.Contains(header.Key))
                    {
                        _httpClient.DefaultRequestHeaders.Remove(header.Key);
                    }

                    if (header.Value is not null)
                    {
                        _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }

            try
            {
                var url = $"http://localhost:{_httpPort}{path}";

                var response = body == null ? await _httpClient.GetAsync(url) : await _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, contentType ?? "application/json"));

                // Skip test by request of the sample app
                if ((int)response.StatusCode == 513)
                {
                    throw new SkipException("HttpStatus code (513) - anticipated flake");
                }

                var responseText = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, responseText);
            }
            catch (HttpRequestException ex)
            {
                return (HttpStatusCode.BadRequest, ex.ToString());
            }
        }

        protected virtual string GetTestName() => _testName;

        protected async Task SendRequestsAsync(params string[] urls)
        {
            foreach (var url in urls)
            {
                await SendRequestsAsyncNoWaitForSpans(url, null, 1);
            }
        }

        protected Task SendRequestsAsync(string url, string body, int numberOfAttacks, int unusedSpanCount, string phase, string contentType, string userAgent)
            => SendRequestsAsyncNoWaitForSpans(url, body, numberOfAttacks, contentType, userAgent);

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
    }
}
