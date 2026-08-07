// <copyright file="AspNetCoreDiagnosticObserverTests.OtelSemantics.cs" company="Datadog">
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
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    /// <summary>
    /// Coverage for <c>DD_TRACE_OTEL_SEMANTICS_ENABLED=true</c>, where the ASP.NET Core integration
    /// emits the OpenTelemetry HTTP semantic convention attributes instead of the Datadog ones.
    /// See https://opentelemetry.io/docs/specs/semconv/http/http-spans/
    /// </summary>
    public partial class AspNetCoreDiagnosticObserverTests
    {
        /// <summary>
        /// Gets the Datadog attributes that must not appear on an OTel-semantics span.
        /// </summary>
        public static TheoryData<string> LegacyHttpTags => new()
        {
            Tags.HttpMethod,
            Tags.HttpUrl,
            Tags.HttpStatusCode,
            Tags.HttpUserAgent,
            "http.request.headers.host",
        };

        /// <summary>
        /// Gets (path, statusCode, isError, expectedRoute, expectedUrlPath, expectedUrlQuery).
        /// The span name is asserted to be <c>{method} {http.route}</c>, or just <c>{method}</c>
        /// when no route was matched — the URI path must never be used as the span name.
        /// </summary>
        public static TheoryData<string, int, bool, string, string, string> OtelSemanticsEndpointRoutingData => new()
        {
            { "/", 200, false, "{controller=home}/{action=index}/{id?}", "/", null },
            { "/Home/Index", 200, false, "{controller=home}/{action=index}/{id?}", "/Home/Index", null },
            { "/Api/Value/3", 200, false, "api/value/{value}", "/Api/Value/3", null },
            { "/echo/123", 200, false, "/echo/{value:int?}", "/echo/123", null },
            { "/echo/123?q=1&token=abc", 200, false, "/echo/{value:int?}", "/echo/123", "q=1&token=abc" },
            { "/healthz", 200, false, "/healthz", "/healthz", null },
            // No route matched: the span name is just the method, never the URI path
            { "/I/dont/123/exist/", 404, false, null, "/I/dont/123/exist/", null },
            { "/Home/Error", 500, true, "{controller=home}/{action=index}/{id?}", "/Home/Error", null },
        };

        [SkippableTheory]
        [MemberData(nameof(OtelSemanticsEndpointRoutingData))]
        public async Task DiagnosticObserver_WithOtelSemantics_EmitsOtelAttributes(
            string path,
            int statusCode,
            bool isError,
            string expectedRoute,
            string expectedUrlPath,
            string expectedUrlQuery)
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>(path, statusCode, isError);

            span.Type.Should().Be(SpanTypes.Web);

            // The span name is "{method} {http.route}", or just "{method}" when there is no route
            span.ResourceName.Should().Be(expectedRoute is null ? "GET" : $"GET {expectedRoute}");
            span.GetTag(Tags.SpanKind).Should().Be(SpanKinds.Server);

            span.GetTag(Tags.HttpRequestMethod).Should().Be("GET");
            span.GetTag(Tags.HttpRequestMethodOriginal).Should().BeNull();
            span.GetTag(Tags.UrlScheme).Should().Be("http");
            span.GetTag(Tags.UrlPath).Should().Be(expectedUrlPath);
            span.GetTag(Tags.UrlQuery).Should().Be(expectedUrlQuery);
            span.GetTag(Tags.ServerAddress).Should().Be("localhost");
            span.GetTag(Tags.HttpResponseStatusCode).Should().Be(statusCode.ToString());
            span.GetHttpStatusCode().Should().Be(statusCode);
            span.GetTag(Tags.HttpRoute).Should().Be(expectedRoute);
            span.Error.Should().Be(isError);
        }

        [SkippableTheory]
        [MemberData(nameof(LegacyHttpTags))]
        public async Task DiagnosticObserver_WithOtelSemantics_DoesNotEmitLegacyAttributes(string legacyTag)
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>("/Api/Value/3", 200, isError: false);

            // http.status_code and http.method are aliases of the OTel names, so GetTag() still resolves
            // them. What matters is that only the OTel name is serialized, which EnumerateTags decides.
            var serialized = GetSerializedTagNames(span);
            serialized.Should().NotContain(legacyTag);
        }

        [SkippableFact]
        public async Task DiagnosticObserver_WithOtelSemantics_SerializesOnlyOtelAttributeNames()
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>("/Api/Value/3?token=abc", 200, isError: false);

            GetSerializedTagNames(span)
               .Should()
               .Contain([Tags.HttpRequestMethod, Tags.UrlScheme, Tags.UrlPath, Tags.UrlQuery, Tags.ServerAddress, Tags.HttpResponseStatusCode, Tags.HttpRoute])
               .And.NotContain([Tags.HttpMethod, Tags.HttpUrl, Tags.HttpStatusCode, "http.request.headers.host"]);
        }

        [SkippableFact]
        public async Task DiagnosticObserver_WithOtelSemantics_RetainsDatadogOnlyAttributes()
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>("/Api/Value/3", 200, isError: false);

            // These have no OpenTelemetry equivalent, so they are retained
            span.GetTag(Tags.AspNetCoreRoute).Should().Be("api/value/{value}");
            span.GetTag(Tags.AspNetCoreEndpoint).Should().NotBeNull();
            span.GetTag(Tags.InstrumentationName).Should().Be("aspnet_core");
        }

        [SkippableTheory]
        [InlineData("PROPFIND")]
        [InlineData("BOGUS")]
        public async Task DiagnosticObserver_WithOtelSemantics_UnknownMethod_IsNormalized(string method)
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>(
                "/Api/Value/3",
                expectedStatusCode: 405,
                isError: false,
                httpMethod: new HttpMethod(method));

            span.GetTag(Tags.HttpRequestMethod).Should().Be("_OTHER");
            span.GetTag(Tags.HttpRequestMethodOriginal).Should().Be(method);

            // {method} MUST be "HTTP" when http.request.method is "_OTHER"
            span.ResourceName.Should().Be("HTTP");
        }

        [SkippableFact]
        public async Task DiagnosticObserver_WithOtelSemantics_LowerCaseMethod_IsCanonicalized()
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>(
                "/Api/Value/3",
                expectedStatusCode: 200,
                isError: false,
                httpMethod: new HttpMethod("get"));

            span.GetTag(Tags.HttpRequestMethod).Should().Be("GET");
            span.GetTag(Tags.HttpRequestMethodOriginal).Should().Be("get");
            span.ResourceName.Should().Be("GET api/value/{value}");
        }

        [SkippableFact]
        public async Task DiagnosticObserver_WithOtelSemantics_ObfuscatesUrlQuery()
        {
            var span = await GetOtelSemanticsServerSpan<EndpointRoutingStartup>(
                "/Api/Value/3?token=sensitive-value&q=1",
                expectedStatusCode: 200,
                isError: false);

            var query = span.GetTag(Tags.UrlQuery);
            query.Should().NotBeNull();
            query.Should().NotStartWith("?");
            query.Should().NotContain("sensitive-value");
            query.Should().Contain("q=1");
        }

        [SkippableFact]
        public async Task DiagnosticObserver_WithOtelSemantics_MvcWithoutEndpointRouting_UsesRouteForSpanName()
        {
            // MvcStartup has endpoint routing disabled, so the route is only recovered when the
            // MVC action is selected. There must still be exactly one span, named from the route.
            var span = await GetOtelSemanticsServerSpan<MvcStartup>("/Home/Index", 200, isError: false);

            span.ResourceName.Should().Be("GET {controller=home}/{action=index}/{id?}");
            span.GetTag(Tags.HttpRoute).Should().Be("{controller=home}/{action=index}/{id?}");
            span.GetTag(Tags.UrlPath).Should().Be("/Home/Index");
        }

        private static IEnumerable<string> GetSerializedTagNames(Span span)
        {
            var processor = new TagNameCollector();
            span.Tags.EnumerateTags(ref processor, span.OpenTelemetrySemanticsEnabled);
            return processor.Names;
        }

        /// <summary>
        /// Runs a single request through the ASP.NET Core diagnostic observer with OTel semantics
        /// enabled, and returns the one and only server span that was produced.
        /// </summary>
        private static async Task<Span> GetOtelSemanticsServerSpan<TStartup>(
            string path,
            int expectedStatusCode,
            bool isError,
            HttpMethod httpMethod = null)
            where TStartup : class
        {
#pragma warning disable ASPDEPR004 // WebHostBuilder is deprecated but we need it for net core 2.1 FIXME
            var builder = new WebHostBuilder().UseStartup<TStartup>();
#pragma warning restore ASPDEPR004

#pragma warning disable ASPDEPR008 // Type or member is obsolete
            using var testServer = new TestServer(builder);
#pragma warning restore ASPDEPR008 // Type or member is obsolete
            using var client = testServer.CreateClient();

            var writer = new AgentWriterStub();
            var configSource = new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.OpenTelemetry.OtelSemanticsEnabled, "true" },
                { ConfigurationKeys.ObfuscationQueryStringRegex, "sensitive-value" },
            });

            await using var tracer = GetTracer(writer, configSource);
            tracer.Settings.OtelSemanticsEnabled.Should().BeTrue();

            var security = new AppSec.Security();
            var iast = new Iast.Iast(new IastSettings(configSource, NullConfigurationTelemetry.Instance), NullDiscoveryService.Instance);
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver(tracer, security, iast, GetSpanCodeOrigin()) };

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                try
                {
                    using var request = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, path);
                    var response = await client.SendAsync(request);
                    response.StatusCode.Should().Be((HttpStatusCode)expectedStatusCode);
                }
                catch (Exception ex)
                {
                    Assert.True(isError, $"Unexpected error calling endpoint: {ex}");
                }

                var deadline = DateTime.Now.AddMilliseconds(30_000);
                while (DateTime.Now < deadline && writer.Traces.Count == 0)
                {
                    await Task.Delay(200);
                }
            }

            var trace = Assert.Single(writer.Traces);

            // OTel semantics generate a single server span: no aspnet_core_mvc.request child
            trace.Should().OnlyContain(x => x.OperationName == "aspnet_core.request");
            return trace.Should().ContainSingle().Subject;
        }

        private struct TagNameCollector : IItemProcessor<string>, IItemProcessor<int>, IItemProcessor<double>, IItemProcessor<byte[]>
        {
            public TagNameCollector()
            {
                Names = [];
            }

            public List<string> Names { get; }

            public void Process(TagItem<string> item) => Names.Add(item.Key);

            public void Process(TagItem<int> item) => Names.Add(item.Key);

            public void Process(TagItem<double> item) => Names.Add(item.Key);

            public void Process(TagItem<byte[]> item) => Names.Add(item.Key);
        }
    }
}

#endif
