// <copyright file="MockAgentTestFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers
{
    public class MockAgentTestFixture : IDisposable
    {
        public MockAgentTestFixture(TestHelper test)
        {
            Test = test;
        }

        public MockAgentTestFixture(TestHelper test, MockTracerAgent agent, int httpPort)
            : this(test)
        {
            Agent = agent;
            HttpPort = httpPort;
        }

        public TestHelper Test { get; }

        public MockTracerAgent Agent { get; set; }

        public int HttpPort { get; set; }

        public HttpClient HttpClient { get; } = new();

        public virtual void Dispose()
        {
            Agent.Dispose();
            HttpClient.Dispose();
        }

        public async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path, string body, string contentType, string userAgent)
        {
            if (!string.IsNullOrEmpty(userAgent) && HttpClient.DefaultRequestHeaders.GetValues("user-agent").All(c => c != userAgent))
            {
                HttpClient.DefaultRequestHeaders.Add("user-agent", userAgent);
            }

            try
            {
                var url = $"http://localhost:{HttpPort}{path}";
                var response =
                    body == null ? await HttpClient.GetAsync(url) : await HttpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, contentType ?? "application/json"));
                var responseText = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, responseText);
            }
            catch (HttpRequestException ex)
            {
                return (HttpStatusCode.BadRequest, ex.ToString());
            }
        }

        public async Task<IImmutableList<MockSpan>> SendRequestsAsync(string url, string body, int numberOfAttacks, int expectedSpans, string phase, string contentType = null, string userAgent = null)
        {
            var minDateTime = DateTime.UtcNow; // when ran sequentially, we get the spans from the previous tests!
            await SendRequestsAsyncNoWaitForSpans(url, body, numberOfAttacks, contentType, userAgent);

            return WaitForSpans(expectedSpans, phase, minDateTime, url);
        }

        public async Task<IImmutableList<MockSpan>> SendRequestsAsync(params string[] urls)
        {
            var spans = new List<MockSpan>();
            foreach (var url in urls)
            {
                spans.AddRange(await SendRequestsAsync(url, null, 1, 1, string.Empty, null));
            }

            return spans.ToImmutableList();
        }

        public async Task SendRequestsAsyncNoWaitForSpans(string url, string body, int numberOfAttacks, string contentType = null, string userAgent = null)
        {
            for (var x = 0; x < numberOfAttacks; x++)
            {
                await SubmitRequest(url, body, contentType, userAgent);
            }
        }

        public IImmutableList<MockSpan> WaitForSpans(int expectedSpans, string phase, DateTime minDateTime, string url)
        {
            Agent.SpanFilters.Clear();
            Agent.SpanFilters.Add(s => s.Tags.ContainsKey("http.url") && s.Tags["http.url"].IndexOf(url, StringComparison.InvariantCultureIgnoreCase) > -1);

            var spans = Agent.WaitForSpans(expectedSpans, minDateTime: minDateTime);
            if (spans.Count != expectedSpans)
            {
                Test.Output?.WriteLine($"spans.Count: {spans.Count} != expectedSpans: {expectedSpans}, this is phase: {phase}");
            }

            return spans;
        }
    }
}
