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
        protected const string Prefix = "Debugger.";
        private readonly HttpClient _httpClient;
        private int _httpPort;

        public AspNetBase(string sampleName, ITestOutputHelper outputHelper, string samplesDir = null)
            : base(Prefix + sampleName, samplesDir ?? "test/test-applications/debugger", outputHelper)
        {
            var handler = new HttpClientHandler();
            _httpClient = new HttpClient(handler);

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

        protected void SetHttpPort(int httpPort) => _httpPort = httpPort;

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path, string body, string contentType, string userAgent = null, string accept = null, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
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
    }
}
