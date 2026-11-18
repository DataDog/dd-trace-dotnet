// <copyright file="AspNetWebFormsElasticsearchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Collection(Elasticsearch6Collection.Name)]
    public class AspNetWebFormsElasticsearchTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebFormsElasticsearchTests(IisFixture iisFixture, Elasticsearch6Fixture elasticsearchFixture, ITestOutputHelper output)
            : base("WebForms", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            ConfigureContainers(elasticsearchFixture);

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/account/login?shutdown=1";
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Trait("SkipInCI", "True")] // This local-only Windows test requires a Linux Elasticsearch container.
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

            var allSpans = (await _iisFixture.Agent.WaitForSpansAsync(3, minDateTime: testStart))
                                   .OrderBy(s => s.Start)
                                   .ToList();

            Assert.True(allSpans.Count > 0, "Expected there to be spans.");
            ValidateIntegrationSpans(allSpans, metadataSchemaVersion: "v0", expectedServiceName: "sample", isExternalSpan: false);

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

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

#endif
