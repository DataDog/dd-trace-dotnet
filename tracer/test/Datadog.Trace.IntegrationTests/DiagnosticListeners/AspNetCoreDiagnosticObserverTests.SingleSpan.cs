// <copyright file="AspNetCoreDiagnosticObserverTests.SingleSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
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
    /// <summary>
    /// This is the single-span observer tests. Moving to a separate class causes issues because the observer uses global state,
    /// so for simplicity,
    /// </summary>
    public partial class AspNetCoreDiagnosticObserverTests
    {
        /// <summary>
        /// Gets data for Razor Pages tests with single-span feature enabled
        /// (URL, StatusCode, isError, Resource, SpanTags)
        /// </summary>
        public static IEnumerable<object[]> RazorPagesSingleSpan => [..AspNetCoreRazorPagesTestData.WithFeatureFlag.Select(x => x[..5])];

        /// <summary>
        /// Gets data for Razor Pages tests with single-span feature enabled and route templates expanded
        /// (URL, StatusCode, isError, Resource, SpanTags)
        /// </summary>
        public static IEnumerable<object[]> RazorPagesSingleSpanWithExpandRouteTemplates => [..AspNetCoreRazorPagesTestData.WithExpandRouteTemplates.Select(x => x[..5])];

        /// <summary>
        /// Gets data for Endpoint routing tests with single-span feature enabled
        /// (URL, StatusCode, isError, Resource, SpanTags)
        /// </summary>
        public static IEnumerable<object[]> EndpointRoutingSingleSpan => [..AspNetCoreEndpointRoutingTestData.WithFeatureFlag.Select(x => x[..5])];

        /// <summary>
        /// Gets data for Endpoint routing tests with single-span feature enabled and route templates expanded
        /// (URL, StatusCode, isError, Resource, SpanTags)
        /// </summary>
        public static IEnumerable<object[]> EndpointRoutingSingleSpanWithExpandRouteTemplates => [..AspNetCoreEndpointRoutingTestData.WithExpandRouteTemplates.Select(x => x[..5])];

        [SkippableTheory]
        [MemberData(nameof(RazorPagesSingleSpan))]
        public async Task SingleSpanDiagnosticObserver_ForRazorPages_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverSubmitsSpans<RazorPagesStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(RazorPagesSingleSpanWithExpandRouteTemplates))]
        public async Task SingleSpanDiagnosticObserver_ForRazorPages_WithExpandedRouteTemplates_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverSubmitsSpans<RazorPagesStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                expandRouteParameters: true);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpan))]
        public async Task SingleSpanDiagnosticObserver_ForEndpointRouting_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpanWithExpandRouteTemplates))]
        public async Task SingleSpanDiagnosticObserver_ForEndpointRouting_WithExpandedRouteTemplates_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverSubmitsSpans<EndpointRoutingStartup>(
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                expandRouteParameters: true);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpan))]
        public async Task SingleSpanDiagnosticObserver_ForWebApplicationBuilder_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: false, path, statusCode, isError, resourceName, expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpan))]
        public async Task SingleSpanDiagnosticObserver_ForWebApplicationBuilder_WithImplicitRouting_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: true, path, statusCode, isError, resourceName, expectedTags);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpanWithExpandRouteTemplates))]
        public async Task SingleSpanDiagnosticObserver_ForWebApplicationBuilder_WithExpandedRouteTemplates_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: false, path, statusCode, isError, resourceName, expectedTags, expandRouteParameters: true);
        }

        [SkippableTheory]
        [MemberData(nameof(EndpointRoutingSingleSpanWithExpandRouteTemplates))]
        public async Task SingleSpanDiagnosticObserver_ForWebApplicationBuilder_WithExpandedRouteTemplates_WithImplicitRouting_SubmitsSpans(string path, int statusCode, bool isError, string resourceName, SerializableDictionary expectedTags)
        {
            await AssertSingleSpanDiagnosticObserverForWebApplicationBuilder(useImplicitRouting: true, path, statusCode, isError, resourceName, expectedTags, expandRouteParameters: true);
        }

        private static async Task AssertSingleSpanDiagnosticObserverForWebApplicationBuilder(
            bool useImplicitRouting,
            string path,
            int statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedTags,
            int childSpanCount = 1,
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

            await AssertSingleSpanDiagnosticObserverSubmitsSpans(
                client,
                path,
                statusCode,
                isError,
                resourceName,
                expectedTags,
                childSpanCount,
                expandRouteParameters);
        }

        private static async Task AssertSingleSpanDiagnosticObserverSubmitsSpans<T>(
            string path,
            int statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedSpanTags,
            int spanCount = 1,
            bool expandRouteParameters = false)
            where T : class
        {
#pragma warning disable ASPDEPR004 // WebHostBuilder is deprecated but we need it for net core 2.1 FIXME
            var builder = new WebHostBuilder()
               .UseStartup<T>();
#pragma warning restore ASPDEPR004

#pragma warning disable ASPDEPR008 // Type or member is obsolete
            var testServer = new TestServer(builder);
#pragma warning restore ASPDEPR008 // Type or member is obsolete
            var client = testServer.CreateClient();

            await AssertSingleSpanDiagnosticObserverSubmitsSpans(
                client,
                path,
                statusCode,
                isError,
                resourceName,
                expectedSpanTags,
                spanCount,
                expandRouteParameters);
        }

        private static async Task AssertSingleSpanDiagnosticObserverSubmitsSpans(
            HttpClient client,
            string path,
            int statusCode,
            bool isError,
            string resourceName,
            SerializableDictionary expectedSpanTags,
            int spanCount = 1,
            bool expandRouteParameters = false)
        {
            var writer = new AgentWriterStub();
            var configSource = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.ExpandRouteTemplatesEnabled, expandRouteParameters.ToString() },
            });
            await using var tracer = GetTracer(writer, configSource);

            var security = new AppSec.Security();
            var iast = new Iast.Iast(new IastSettings(configSource, NullConfigurationTelemetry.Instance), NullDiscoveryService.Instance);
            var spanOrigin = GetSpanCodeOrigin();
            var observers = new List<DiagnosticObserver> { new SingleSpanAspNetCoreDiagnosticObserver(tracer, security, iast, spanOrigin) };

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                try
                {
                    var response = await client.GetAsync(path);
                    response.StatusCode.Should().Be((HttpStatusCode)statusCode);
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

                    await Task.Delay(200);
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
            AssertTagHasValue(parentSpan, Tags.HttpStatusCode, statusCode.ToString());
            parentSpan.Error.Should().Be(isError);

            if (expectedSpanTags is not null)
            {
                foreach (var expectedTag in expectedSpanTags.Values)
                {
                    AssertTagHasValue(parentSpan, expectedTag.Key, expectedTag.Value);
                }
            }
        }
    }
}
#endif
