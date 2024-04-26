// <copyright file="AspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public class AspNetCoreDiagnosticObserverTests
    {
        [SkippableTheory]
        [MemberData(nameof(AspNetCoreMvcTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreMvcTestData))]
        public async Task DiagnosticObserver_ForMvcEndpoints_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<MvcStartup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreMvcTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreMvcTestData))]
        public async Task DiagnosticObserver_ForMvcEndpoints_WithFeatureFlag_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<MvcStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreMvcTestData.WithExpandRouteTemplates), MemberType = typeof(AspNetCoreMvcTestData))]
        public async Task DiagnosticObserver_ForMvcEndpoints_WithExpandedRouteTemplates_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<MvcStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags,
                expandRouteParameters: true);
        }

#if NETCOREAPP3_0_OR_GREATER
        [SkippableTheory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<RazorPagesStartup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_WithFeatureFlag_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<RazorPagesStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreRazorPagesTestData.WithExpandRouteTemplates), MemberType = typeof(AspNetCoreRazorPagesTestData))]
        public async Task DiagnosticObserver_ForRazorPages_WithExpandedRouteTemplates_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<RazorPagesStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags,
                expandRouteParameters: true);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(path, statusCode, isError, resourceName, expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_WithFeatureFlag_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithExpandRouteTemplates), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForEndpointRouting_WithExpandedRouteTemplates_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlag: true,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags,
                expandRouteParameters: true);
        }
#endif

#if NET6_0_OR_GREATER
        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: false, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: false);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithoutFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_WithImplicitRouting_SubmitsSpans(string path, HttpStatusCode statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: true, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: false);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_WithFeatureFlag_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: false, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: true, childSpanCount, childSpan1ResourceName, firstChildSpanTags, childSpan2ResourceName, secondChildSpanTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithFeatureFlag), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_WithFeatureFlag_WithImplicitRouting_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: true, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: true, childSpanCount, childSpan1ResourceName, firstChildSpanTags, childSpan2ResourceName, secondChildSpanTags);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithExpandRouteTemplates), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_WithExpandedRouteTemplates_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: false, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: true, childSpanCount, childSpan1ResourceName, firstChildSpanTags, childSpan2ResourceName, secondChildSpanTags, expandRouteParameters: true);
        }

        [SkippableTheory]
        [MemberData(nameof(AspNetCoreEndpointRoutingTestData.WithExpandRouteTemplates), MemberType = typeof(AspNetCoreEndpointRoutingTestData))]
        public async Task DiagnosticObserver_ForWebApplicationBuilder_WithExpandedRouteTemplates_WithImplicitRouting_SubmitsSpans(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount,
            string childSpan1ResourceName,
            SerializableDictionary firstChildSpanTags,
            string childSpan2ResourceName,
            SerializableDictionary secondChildSpanTags)
        {
            await AssertDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: true, path, statusCode, isError, resourceName, expectedTags, featureFlagEnabled: true, childSpanCount, childSpan1ResourceName, firstChildSpanTags, childSpan2ResourceName, secondChildSpanTags, expandRouteParameters: true);
        }

        private static async Task AssertDiagnosticObserverForWebApplicationBuilder(
            bool useImplicitRouting,
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            bool featureFlagEnabled,
            int childSpanCount = 1,
            string childSpan1ResourceName = null,
            SerializableDictionary firstChildSpanTags = null,
            string childSpan2ResourceName = null,
            SerializableDictionary secondChildSpanTags = null,
            bool expandRouteParameters = false)
        {
            var startup = new EndpointRoutingStartup();
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
            startup.ConfigureServices(builder.Services);

            var startupFilter = ErrorHandlingHelper.GetStartupFilter(path);
            if (startupFilter is not null && useImplicitRouting)
            {
                builder.Services.AddSingleton<IStartupFilter>(startupFilter);
            }

            builder.WebHost.UseTestServer();

            var app = builder.Build();
            ErrorHandlingHelper.AddErrorHandlerInline(app, path);
            if (!useImplicitRouting)
            {
                app.UseRouting();
            }

            EndpointRoutingStartup.ConfigureEndpoints(app);

            await app.StartAsync();

            var testServer = (TestServer)app.Services.GetService(typeof(IServer));
            var client = testServer.CreateClient();

            await AssertDiagnosticObserverSubmitsSpans(
                client,
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                featureFlagEnabled,
                childSpanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags,
                expandRouteParameters);
        }
#endif

        private static async Task AssertDiagnosticObserverSubmitsSpans<T>(
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedParentSpanTags,
            bool featureFlag = false,
            int spanCount = 1,
            string childSpan1ResourceName = null,
            SerializableDictionary firstChildSpanTags = null,
            string childSpan2ResourceName = null,
            SerializableDictionary secondChildSpanTags = null,
            bool expandRouteParameters = false)
            where T : class
        {
            var builder = new WebHostBuilder()
               .UseStartup<T>();

            var testServer = new TestServer(builder);
            var client = testServer.CreateClient();

            await AssertDiagnosticObserverSubmitsSpans(
                client,
                path,
                statusCode,
                isError,
                resourceName,
                expectedParentSpanTags,
                featureFlag,
                spanCount,
                childSpan1ResourceName,
                firstChildSpanTags,
                childSpan2ResourceName,
                secondChildSpanTags,
                expandRouteParameters);
        }

        private static async Task AssertDiagnosticObserverSubmitsSpans(
            HttpClient client,
            string path,
            HttpStatusCode statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedParentSpanTags,
            bool featureFlag = false,
            int spanCount = 1,
            string childSpan1ResourceName = null,
            SerializableDictionary firstChildSpanTags = null,
            string childSpan2ResourceName = null,
            SerializableDictionary secondChildSpanTags = null,
            bool expandRouteParameters = false)
        {
            var writer = new AgentWriterStub();
            var configSource = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, featureFlag.ToString() },
                { ConfigurationKeys.ExpandRouteTemplatesEnabled, expandRouteParameters.ToString() },
            });
            var tracer = GetTracer(writer, configSource);

            var security = new AppSec.Security();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver(tracer, security) };

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
                const int timeoutInMilliseconds = 30_000;

                var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
                while (DateTime.Now < deadline)
                {
                    if (writer.Traces.Count > 0)
                    {
                        break;
                    }

                    Thread.Sleep(200);
                }
            }

            var trace = Assert.Single(writer.Traces);
            trace.Should().HaveCount(spanCount);

            var parentSpan = trace.Should()
                                  .ContainSingle(x => x.OperationName == "aspnet_core.request")
                                  .Subject;

            AssertTagHasValue(parentSpan, Tags.InstrumentationName, "aspnet_core");
            parentSpan.Type.Should().Be(SpanTypes.Web);
            parentSpan.ResourceName.Should().Be(resourceName);
            AssertTagHasValue(parentSpan, Tags.SpanKind, SpanKinds.Server);
            AssertTagHasValue(parentSpan, Tags.HttpStatusCode, ((int)statusCode).ToString());
            parentSpan.Error.Should().Be(isError);

            if (expectedParentSpanTags is not null)
            {
                foreach (var expectedTag in expectedParentSpanTags.Values)
                {
                    AssertTagHasValue(parentSpan, expectedTag.Key, expectedTag.Value);
                }
            }

            if (spanCount > 1)
            {
                trace.Should().Contain(x => x.OperationName == "aspnet_core_mvc.request");

                var childSpan = trace.First(x => x.OperationName == "aspnet_core_mvc.request");

                AssertTagHasValue(childSpan, Tags.InstrumentationName, "aspnet_core");
                childSpan.Type.Should().Be(SpanTypes.Web);
                childSpan.ResourceName.Should().Be(childSpan1ResourceName ?? resourceName);
                AssertTagHasValue(childSpan, Tags.SpanKind, SpanKinds.Server);

                if (firstChildSpanTags is not null)
                {
                    foreach (var expectedTag in firstChildSpanTags.Values)
                    {
                        AssertTagHasValue(childSpan, expectedTag.Key, expectedTag.Value);
                    }
                }

                if (spanCount > 2)
                {
                    var childSpan2 = trace.Last(x => x.OperationName == "aspnet_core_mvc.request");
                    childSpan2.Should().NotBe(childSpan);

                    AssertTagHasValue(childSpan2, Tags.InstrumentationName, "aspnet_core");
                    childSpan2.Type.Should().Be(SpanTypes.Web);
                    childSpan2.ResourceName.Should().Be(childSpan2ResourceName);
                    AssertTagHasValue(childSpan2, Tags.SpanKind, SpanKinds.Server);

                    if (secondChildSpanTags is not null)
                    {
                        foreach (var expectedTag in secondChildSpanTags.Values)
                        {
                            AssertTagHasValue(childSpan2, expectedTag.Key, expectedTag.Value);
                        }
                    }
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
            var samplerMock = new Mock<ITraceSampler>();

            return new Tracer(settings, agentWriter, samplerMock.Object, scopeManager: null, statsd: null);
        }

        private class AgentWriterStub : IAgentWriter
        {
            public List<ArraySegment<Span>> Traces { get; } = new();

            public Task FlushAndCloseAsync() => Task.CompletedTask;

            public Task FlushTracesAsync() => Task.CompletedTask;

            public Task<bool> Ping() => Task.FromResult(true);

            public void WriteTrace(ArraySegment<Span> trace) => Traces.Add(trace);
        }
    }
}

#endif
