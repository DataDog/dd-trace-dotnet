using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Internal;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SpanDecorationTests
    {
        private const string DefaultHost = "SpanTests.TestHost.DataDogDemo.com";

        private static readonly Dictionary<string, string> DefaultHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                                                            {
                                                                                { "Host", DefaultHost },
                                                                                { "Content-Type", "application/json" },
                                                                                { "ETag", "8675309eyine8675309eyine" },
                                                                                { "X-Datadog-Test", "thisIsATest" }
                                                                            };

        private static readonly ISpanDecorationService DecorationService = new CompositeSpanDecorationService(
                                                                                                              TagsSpanDecorationService.Instance,
                                                                                                              TypeSpanDecorationService.Instance,
                                                                                                              ResourceNameDecorationService.Instance);

        [Fact]
        public void DefaultHttpSpanTagProducerProducesExpectedTags()
        {
            const string testUrl = "https://demotest.DataDogDemo.com/PathSegment1/PathSegment2?queryStringParam1=qsp1Val";

            var decorationSource = new TestHttpDecorationSource(testUrl, "PoSt", DefaultHeaders);

            var tags = HttpSpanTagsProducer.Instance.GetTags(decorationSource).ToList();

            Assert.False(tags.IsNullOrEmpty(), "Tags are null/empty");
            Assert.True(tags.Count == 3, "Tag count is incorrect");
            Assert.True(tags.Single(t => t.Key.Equals(Tags.HttpMethod, StringComparison.Ordinal)).Value.Equals("POST", StringComparison.Ordinal), "HttpMethod tag is incorrect");
            Assert.True(tags.Single(t => t.Key.Equals(Tags.HttpRequestHeadersHost, StringComparison.Ordinal)).Value.Equals(DefaultHost, StringComparison.Ordinal), "HttpRequestHeadersHost tag is incorrect");
            Assert.True(tags.Single(t => t.Key.Equals(Tags.HttpUrl, StringComparison.Ordinal)).Value.Equals(testUrl.ToLowerInvariant(), StringComparison.Ordinal), "HttpUrl tag is incorrect");
        }

        [Fact]
        public void DefaultServiceAndSourceWithValidValuesIsSuccessfull()
        {
            const string testUrl = "https://demotest.DataDogDemo.com/PathSegment1/PathSegment2?queryStringParam1=qsp1Val";

            var decorationSource = new TestHttpDecorationSource(testUrl, "PuT", DefaultHeaders);

            var span = new TestSpan();

            DecorationService.Decorate(span, decorationSource);

            Assert.True(span.Type.Equals(SpanTypes.Web, StringComparison.Ordinal), "Type");
            Assert.True(span.GetTag(Tags.HttpMethod).Equals("PUT", StringComparison.Ordinal), "HttpMethod");
            Assert.True(span.GetHttpMethod().Equals("PUT", StringComparison.Ordinal), "GetHttpMethod");
            Assert.True(span.GetTag(Tags.HttpRequestHeadersHost).Equals(DefaultHost, StringComparison.Ordinal), "HttpRequestHeadersHost");
            Assert.True(span.GetTag(Tags.HttpUrl).Equals(testUrl.ToLowerInvariant(), StringComparison.Ordinal), "HttpUrl");
        }

        [Fact]
        public void DefaultServiceAndSourceWithMissingValuesIsSuccessfull()
        {
            const string testUrl = "https://demotest.DataDogDemo.com/PathSegment1/PathSegment2?queryStringParam1=qsp1Val";

            var headers = new Dictionary<string, string>(DefaultHeaders, StringComparer.OrdinalIgnoreCase);
            headers.Remove("Host");

            var decorationSource = new TestHttpDecorationSource(testUrl, null, headers);

            var span = new TestSpan();

            DecorationService.Decorate(span, decorationSource);

            Assert.True(span.Type.Equals(SpanTypes.Web, StringComparison.Ordinal), "Type");
            Assert.True(span.GetTag(Tags.HttpMethod).Equals("GET", StringComparison.Ordinal), "HttpMethod");
            Assert.True(span.GetHttpMethod().Equals("GET", StringComparison.Ordinal), "GetHttpMethod");
            Assert.True(span.GetTag(Tags.HttpRequestHeadersHost) == null, "HttpRequestHeadersHost");
            Assert.True(span.GetTag(Tags.HttpUrl).Equals(testUrl.ToLowerInvariant(), StringComparison.Ordinal), "HttpUrl");
        }

        private class TestHttpDecorationSource : ISpanDecorationSource, IHttpSpanTagsSource
        {
            private readonly string _url;
            private readonly string _httpMethod;
            private readonly Dictionary<string, string> _headers;

            public TestHttpDecorationSource(string url, string httpMethod, Dictionary<string, string> headers)
            {
                _url = url;
                _httpMethod = httpMethod;
                _headers = headers ?? new Dictionary<string, string>();
            }

            public IEnumerable<KeyValuePair<string, string>> GetTags()
                => HttpSpanTagsProducer.Instance.GetTags(this);

            public bool TryGetResourceName(out string resourceName)
            {
                resourceName = null;

                return false;
            }

            public bool TryGetType(out string spanType)
            {
                spanType = SpanTypes.Web;

                return true;
            }

            public string GetHttpMethod() => _httpMethod;

            public string GetHttpHost() => _headers.TryGetValue("Host", out var headerValue)
                                               ? headerValue
                                               : null;

            public string GetHttpUrl() => _url;
        }
    }
}
