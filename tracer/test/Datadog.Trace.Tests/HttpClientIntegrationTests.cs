using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Xunit;
using Xunit.Abstractions;
using Datadog.Trace.TestHelpers; // Assuming TestServer is part of the TestHelpers
using Microsoft.AspNetCore.TestHost; // Added to use TestServer

namespace Datadog.Trace.Tests.HttpClientIntegration
{
    public class HttpClientIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Tracer _tracer;

        public HttpClientIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            var settings = TracerSettings.FromDefaultSources();
            _tracer = Tracer.Configure(settings);
        }

        [Fact]
        public async Task SubmitsTraces()
        {
            // Arrange
            using var testServer = new TestServer(new WebHostBuilder().UseStartup<Startup>()); // Assuming Startup class configures services and middleware
            using var client = testServer.CreateClient();

            // Act
            var response = await client.GetAsync("/"); // Assuming the test server is configured to handle requests to the root URL

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var spans = _tracer.DistributedSpanContext.GetSpans(); // This method needs to be verified or replaced with the correct way to retrieve spans
            Assert.Single(spans);
            var span = spans[0];

            Assert.Equal("http.request", span.OperationName);
            Assert.Equal(testServer.BaseAddress.ToString(), span.Tags[Tags.HttpUrl]);
            Assert.Equal("GET", span.Tags[Tags.HttpMethod]);
            Assert.Equal("200", span.Tags[Tags.HttpStatusCode]);
        }
    }
}
