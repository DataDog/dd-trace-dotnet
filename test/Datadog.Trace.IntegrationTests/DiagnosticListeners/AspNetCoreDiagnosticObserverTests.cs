#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Moq;
using Xunit;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public class AspNetCoreDiagnosticObserverTests
    {
        [Theory]
        [MemberData(nameof(AspNetCoreMvcTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreMvcTestData))]
        public async Task DiagnosticObserver_ForMvcEndpoints_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<MvcStartup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [Theory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<Samples.AspNetCoreRazorPages.Startup>(path, statusCode, isError, resourceName, expectedTags);
        }

#if !NETCOREAPP2_1
        [Theory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(path, statusCode, isError, resourceName, expectedTags);
        }
#endif

        private static async Task AssertDiagnosticObserverSubmitsSpans<T>(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags)
            where T : class
        {
            var writer = new AgentWriterStub();
            var tracer = GetTracer(writer);

            var builder = new WebHostBuilder()
               .UseStartup<T>();

            var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver(tracer) };

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                try
                {
                    var response = await client.GetAsync(path);
                    Assert.Equal(statusCode, response.StatusCode);
                }
                catch (Exception ex)
                {
                    Assert.True(isError, $"Unexpected error calling endpoint: {ex}");
                }

                // The diagnostic observer runs on a separate thread
                // This gives time for the Stop event to run and to be flushed to the writer
                var iterations = 10;
                while (iterations > 0)
                {
                    if (writer.Traces.Count > 0)
                    {
                        break;
                    }

                    Thread.Sleep(10);
                    iterations--;
                }
            }

            var trace = Assert.Single(writer.Traces);
            var span = Assert.Single(trace);

            Assert.Equal("aspnet_core.request", span.OperationName);
            Assert.Equal("aspnet_core", span.GetTag(Tags.InstrumentationName));
            Assert.Equal(SpanTypes.Web, span.Type);
            Assert.Equal(resourceName, span.ResourceName);
            Assert.Equal(SpanKinds.Server, span.GetTag(Tags.SpanKind));
            Assert.Equal(TracerConstants.Language, span.GetTag(Tags.Language));
            Assert.Equal(((int)statusCode).ToString(), span.GetTag(Tags.HttpStatusCode));
            Assert.Equal(isError, span.Error);

            if (expectedTags is not null)
            {
                foreach (var expectedTag in expectedTags.Values)
                {
                    Assert.Equal(expectedTag.Value, span.Tags.GetTag(expectedTag.Key));
                }
            }
        }

        private static Tracer GetTracer(ITraceWriter writer = null)
        {
            var settings = new TracerSettings();
            var agentWriter = writer ?? new Mock<ITraceWriter>().Object;
            var samplerMock = new Mock<ISampler>();

            return new Tracer(settings, agentWriter, samplerMock.Object, scopeManager: null, statsd: null);
        }

        private class AgentWriterStub : ITraceWriter
        {
            public List<Span[]> Traces { get; } = new();

            public Task FlushAndCloseAsync() => Task.CompletedTask;

            public Task FlushTracesAsync() => Task.CompletedTask;

            public Task<bool> Ping() => Task.FromResult(true);

            public void WriteTrace(Span[] trace) => Traces.Add(trace);
        }
    }
}

#endif
