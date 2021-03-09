#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
        [MemberData(nameof(AspNetCoreMvcTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreMvcTestData))]
        public async Task DiagnosticObserver_ForMvcEndpoints_WithFeatureFlag_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<MvcStartup>(path, statusCode, isError, resourceName, expectedTags, featureFlag: true);
        }

        [Theory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<Samples.AspNetCoreRazorPages.Startup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [Theory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_WithFeatureFlag_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<Samples.AspNetCoreRazorPages.Startup>(path, statusCode, isError, resourceName, expectedTags, featureFlag: true);
        }

#if !NETCOREAPP2_1
        [Theory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [Theory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_WithFeatureFlag_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(path, statusCode, isError, resourceName, expectedTags, featureFlag: true);
        }
#endif

        private static async Task AssertDiagnosticObserverSubmitsSpans<T>(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            bool featureFlag = false)
            where T : class
        {
            var writer = new AgentWriterStub();
            var configSource = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.FeatureFlags.AspNetCoreRouteTemplateResourceNamesEnabled, featureFlag.ToString() },
            });
            var tracer = GetTracer(writer, configSource);

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

            span.OperationName.Should().Be("aspnet_core.request");
            AssertTagHasValue(span, Tags.InstrumentationName, "aspnet_core");
            span.Type.Should().Be(SpanTypes.Web);
            span.ResourceName.Should().Be(resourceName);
            AssertTagHasValue(span, Tags.SpanKind, SpanKinds.Server);
            AssertTagHasValue(span, Tags.Language, TracerConstants.Language);
            AssertTagHasValue(span, Tags.HttpStatusCode, ((int)statusCode).ToString());
            span.Error.Should().Be(isError);

            if (expectedTags is not null)
            {
                foreach (var expectedTag in expectedTags.Values)
                {
                    AssertTagHasValue(span, expectedTag.Key, expectedTag.Value);
                }
            }
        }

        private static void AssertTagHasValue(Span span, string tagName, string expected)
        {
            span.GetTag(tagName).Should().Be(expected, $"'{tagName}' should have correct value");
        }

        private static Tracer GetTracer(IAgentWriter writer = null, IConfigurationSource configSource = null)
        {
            var settings = new TracerSettings(configSource);
            var agentWriter = writer ?? new Mock<IAgentWriter>().Object;
            var samplerMock = new Mock<ISampler>();

            return new Tracer(settings, agentWriter, samplerMock.Object, scopeManager: null, statsd: null);
        }

        private class AgentWriterStub : IAgentWriter
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
