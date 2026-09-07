// <copyright file="NinjectMvcCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class NinjectMvcCacheTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private const int Iterations = 750;
        private const int ValueTypeProperties = 16;
        private readonly IisFixture _iisFixture;

        public NinjectMvcCacheTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("NinjectMvcCache", @"test\test-applications\aspnet", output)
        {
            _iisFixture = iisFixture;
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task InstrumentedRequestPopulatesDegenerateActivationCache()
        {
            var testStart = DateTime.UtcNow;
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var url = $"http://localhost:{_iisFixture.HttpPort}/Stress/Run?iterations={Iterations}&includeCache=true";

            var response = await client.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"[http] {response.StatusCode} {responseBody}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"server returned an error: {responseBody}");

            var result = JObject.Parse(responseBody);
            result.Value<string>("Mode").Should().Be("NinjectValidator");

            var cache = result["Cache"];
            cache.Should().NotBeNull();
            cache.Value<bool>("Disabled").Should().BeFalse();
            cache.Value<int>("RequiredAttributeCount").Should().BeGreaterThanOrEqualTo(Iterations * ValueTypeProperties);
            cache.Value<int>("LargestHashGroup").Should().BeGreaterThanOrEqualTo(Iterations * ValueTypeProperties);
            cache.Value<double>("LargestHashGroupPercentage").Should().BeGreaterThan(80);

            var spans = await _iisFixture.Agent.WaitForSpansAsync(2, minDateTime: testStart, returnAllOperations: true);
            spans.Should().HaveCount(2);
            spans.Select(span => span.Name).Should().BeEquivalentTo("aspnet.request", "aspnet-mvc.request");
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "sample", isExternalSpan: false);
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

#endif
