// <copyright file="AspNetWebFormsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebFormsTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        // NOTE: Would pass this in addition to the name/output to the new constructor if we removed the Samples.WebForms copied project in favor of the demo repo source project...
        // $"../dd-trace-demo/dotnet-coffeehouse/Datadog.Coffeehouse.WebForms",
        public AspNetWebFormsTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("WebForms", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/account/login?shutdown=1";
            _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [InlineData("/Account/Login", "GET /account/login", false)]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            bool isError)
        {
            await AssertAspNetSpanOnly(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                isError,
                expectedErrorType: null,
                expectedErrorMessage: null,
                SpanTypes.Web,
                expectedResourceName,
                "1.0.0");
        }

        [Fact(Skip = "This test requires Elasticsearch to be running on the host, which is not currently enabled in CI.")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task NestedAsyncElasticCallSubmitsTrace()
        {
            var testStart = DateTime.UtcNow;
            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

                var response = await httpClient.GetAsync($"http://localhost:{_iisFixture.HttpPort}" + "/Database/Elasticsearch");
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var allSpans = _iisFixture.Agent.WaitForSpans(3, minDateTime: testStart)
                                   .OrderBy(s => s.Start)
                                   .ToList();

            Assert.True(allSpans.Count > 0, "Expected there to be spans.");

            var aspnetSpans = allSpans.Where(s => s.Name == "aspnet.request");
            foreach (var aspnetSpan in aspnetSpans)
            {
                var result = aspnetSpan.IsAspNet();
                Assert.True(result.Success, result.ToString());
            }

            var aspnetMvcSpans = allSpans.Where(s => s.Name == "aspnet-mvc.request");
            foreach (var aspnetMvcSpan in aspnetMvcSpans)
            {
                var result = aspnetMvcSpan.IsAspNetMvc();
                Assert.True(result.Success, result.ToString());
            }

            var elasticSpans = allSpans
                             .Where(s => s.Type == "elasticsearch")
                             .ToList();

            Assert.True(elasticSpans.Count > 0, "Expected elasticsearch spans.");

            foreach (var span in elasticSpans)
            {
                Assert.Equal("elasticsearch.query", span.Name);
                Assert.Equal("Development Web Site-elasticsearch", span.Service);
                Assert.Equal("elasticsearch", span.Type);
            }
        }
    }
}

#endif
