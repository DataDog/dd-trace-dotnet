using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ScopeFactoryTests
    {
        // declare here instead of using ScopeFactory.UrlIdPlaceholder so tests fails if value changes
        private const string Id = "?";

        [Theory]
        [InlineData("users/", "users/")]
        [InlineData("users", "users")]
        [InlineData("123/", Id + "/")]
        [InlineData("123", Id)]
        [InlineData("4294967294/", Id + "/")]
        [InlineData("4294967294", Id)]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852-227B-4F0C-9E48-D30D83C68BF3", Id)]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3/", Id + "/")]
        [InlineData("E653C852227B4F0C9E48D30D83C68BF3", Id)]
        public void CleanUriSegment(string segment, string expected)
        {
            string actual = UriHelpers.CleanUriSegment(segment);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "GET", "GET example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/123/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/123", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/4294967294/file.aspx", "GET", "GET example.com/path/" + Id + "/file.aspx")]
        [InlineData("https://example.com/path/4294967294/", "GET", "GET example.com/path/" + Id + "/")]
        [InlineData("https://example.com/path/4294967294", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "GET", "GET example.com/path/" + Id)]
        public void CleanUri_ResourceName(string uri, string method, string expected)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string integrationName = "HttpMessageHandler";

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), integrationName))
            {
                Assert.Equal(expected, automaticScope.Span.ResourceName);
            }
        }

        [Theory]
        [InlineData("https://username:password@example.com/path/to/file.aspx?query=1#fragment", "https://example.com/path/to/file.aspx")]
        [InlineData("https://username@example.com/path/to/file.aspx", "https://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx?query=1", "https://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/to/file.aspx#fragment", "https://example.com/path/to/file.aspx")]
        [InlineData("http://example.com/path/to/file.aspx", "http://example.com/path/to/file.aspx")]
        [InlineData("https://example.com/path/123/file.aspx", "https://example.com/path/123/file.aspx")]
        [InlineData("https://example.com/path/123/", "https://example.com/path/123/")]
        [InlineData("https://example.com/path/123", "https://example.com/path/123")]
        [InlineData("https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3", "https://example.com/path/E653C852-227B-4F0C-9E48-D30D83C68BF3")]
        [InlineData("https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3", "https://example.com/path/E653C852227B4F0C9E48D30D83C68BF3")]
        public void CleanUri_HttpUrlTag(string uri, string expected)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";
            const string integrationName = "HttpMessageHandler";

            using (var automaticScope = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(uri), integrationName))
            {
                Assert.Equal(expected, automaticScope.Span.GetTag(Tags.HttpUrl));
            }
        }

        [Theory]
        [InlineData("HttpMessageHandler", "HttpMessageHandler")] // This scenario may occur on any .NET runtime with nested HttpMessageHandler's and HttpSocketHandler's
        [InlineData("WebRequest", "HttpMessageHandler")] // This scenario may occur on .NET Core where the underlying transport for WebRequest is HttpMessageHandler
        public void CreateOutboundHttpScope_AlwaysCreatesOneAutomaticInstrumentationScope(string integrationName1, string integrationName2)
        {
            // Set up Tracer
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();
            var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            const string method = "GET";
            const string url = "http://www.contoso.com";

            // Manually create a span decorated with HTTP information
            using (var manualScope = tracer.StartActive("http.request"))
            {
                manualScope.Span.Type = SpanTypes.Http;
                manualScope.Span.ResourceName = $"{method} {url}";
                manualScope.Span.ServiceName = $"{tracer.DefaultServiceName}-http-client";

                using (var automaticScope1 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), integrationName1))
                {
                    using (var automaticScope2 = ScopeFactory.CreateOutboundHttpScope(tracer, method, new Uri(url), integrationName2))
                    {
                        Assert.NotNull(manualScope);
                        Assert.NotNull(automaticScope1);
                        Assert.Null(automaticScope2);
                    }
                }
            }
        }
    }
}
