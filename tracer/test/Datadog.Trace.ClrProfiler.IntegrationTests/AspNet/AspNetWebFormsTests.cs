// <copyright file="AspNetWebFormsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461 || NET452

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetWebFormsTests : IisTestsBase
    {
        // NOTE: Would pass this in addition to the name/output to the new constructor if we removed the Samples.WebForms copied project in favor of the demo repo source project...
        // $"../dd-trace-demo/dotnet-coffeehouse/Datadog.Coffeehouse.WebForms",
        public AspNetWebFormsTests()
            : base("WebForms", @"test\test-applications\aspnet", IisAppType.AspNetIntegrated, "/account/login?shutdown=1")
        {
            SetServiceVersion("1.0.0");
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("LoadFromGAC", "True")]
        [TestCase("/Account/Login", "GET /account/login", false)]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            bool isError)
        {
            await AssertAspNetSpanOnly(
                path,
                Agent,
                HttpPort,
                HttpStatusCode.OK,
                isError,
                expectedErrorType: null,
                expectedErrorMessage: null,
                SpanTypes.Web,
                expectedResourceName,
                "1.0.0");
        }

        [Test]
        [Ignore("This test requires Elasticsearch to be running on the host, which is not currently enabled in CI.")]
        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("LoadFromGAC", "True")]
        public async Task NestedAsyncElasticCallSubmitsTrace()
        {
            var testStart = DateTime.UtcNow;
            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

                var response = await httpClient.GetAsync($"http://localhost:{HttpPort}" + "/Database/Elasticsearch");
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            var allSpans = Agent.WaitForSpans(3, minDateTime: testStart)
                                   .OrderBy(s => s.Start)
                                   .ToList();

            Assert.True(allSpans.Count > 0, "Expected there to be spans.");

            var elasticSpans = allSpans
                             .Where(s => s.Type == "elasticsearch")
                             .ToList();

            Assert.True(elasticSpans.Count > 0, "Expected elasticsearch spans.");

            foreach (var span in elasticSpans)
            {
                Assert.AreEqual("elasticsearch.query", span.Name);
                Assert.AreEqual("Development Web Site-elasticsearch", span.Service);
                Assert.AreEqual("elasticsearch", span.Type);
            }
        }
    }
}

#endif
