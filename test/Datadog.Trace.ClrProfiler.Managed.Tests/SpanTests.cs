using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SpanTests
    {
        private const string DefaultHost = "SpanTests.TestHost.DataDogDemo.com";

        private static readonly Dictionary<string, string> DefaultHeaders = new Dictionary<string, string>
                                                                            {
                                                                                { "Host", DefaultHost },
                                                                                { "Content-Type", "application/json" },
                                                                                { "ETag", "8675309eyine8675309eyine" },
                                                                                { "X-Datadog-Test", "thisIsATest" }
                                                                            };

        [Fact]
        public void DefaultContextDecoratorWithValidValuesIsSuccessfull()
        {
            const string testUrl = "https://demotest.DataDogDemo.com/PathSegment1/PathSegment2?queryStringParam1=qsp1Val";

            var contextAdapter = new TestHttpContextAdapter(testUrl, "gEt", DefaultHeaders);

            var decorator = DefaultSpanDecorationBuilder.Create()
                                                        .With(contextAdapter.AllWebSpanDecorator())
                                                        .Build();

            var span = new TestSpan();

            span.DecorateWith(decorator);

            Assert.True(span.Type.Equals(SpanTypes.Web, StringComparison.Ordinal), "Type");
            Assert.True(span.Tags[Tags.HttpMethod].Equals("GET", StringComparison.Ordinal), $"HttpMethod is [{span.Tags[Tags.HttpMethod]}]");
            Assert.True(span.GetHttpMethod().Equals("GET", StringComparison.Ordinal), $"GetHttpMethod is [{span.Tags[Tags.HttpMethod]}]");
            Assert.True(span.Tags[Tags.HttpRequestHeadersHost].Equals(DefaultHost, StringComparison.Ordinal), "HttpRequestHeadersHost");
            Assert.True(span.Tags[Tags.HttpUrl].Equals(testUrl.ToLowerInvariant(), StringComparison.Ordinal), "HttpUrl");
        }

        [Fact]
        public void DefaultContextDecoratorWithMissingValuesIsSuccessfull()
        {
            const string testUrl = "https://demotest.DataDogDemo.com/PathSegment1/PathSegment2?queryStringParam1=qsp1Val";

            var headers = DefaultHeaders;
            headers.Remove("Host");

            var contextAdapter = new TestHttpContextAdapter(testUrl, "GET", headers);

            var decorator = DefaultSpanDecorationBuilder.Create()
                                                        .With(contextAdapter.AllWebSpanDecorator())
                                                        .Build();

            var span = new TestSpan();

            span.DecorateWith(decorator);

            Assert.True(span.Type.Equals(SpanTypes.Web, StringComparison.Ordinal), "Type");
            Assert.True(span.Tags[Tags.HttpMethod].Equals("GET", StringComparison.Ordinal), "HttpMethod");
            Assert.True(!span.Tags.ContainsKey(Tags.HttpRequestHeadersHost), "HttpRequestHeadersHost");
            Assert.True(span.Tags[Tags.HttpUrl].Equals(testUrl.ToLowerInvariant(), StringComparison.Ordinal), "HttpUrl");
        }

        private class TestHttpContextAdapter : IHttpSpanDecoratable
        {
            private readonly string _url;
            private readonly string _httpMethod;
            private readonly Dictionary<string, string> _headers;

            public TestHttpContextAdapter(string url, string httpMethod, Dictionary<string, string> headers)
            {
                _url = url;
                _httpMethod = httpMethod;
                _headers = headers ?? new Dictionary<string, string>();
            }

            public string GetRawUrl() => _url;

            public string GetHeaderValue(string headerName) => _headers.TryGetValue(headerName, out var headerValue)
                                                                   ? headerValue
                                                                   : null;

            public string GetHttpMethod() => _httpMethod;
        }

        private class TestSpan : ISpan
        {
            public bool Error { get; set; }

            public string ResourceName { get; set; }

            public string Type { get; set; }

            internal ConcurrentDictionary<string, string> Tags { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string GetTag(string key) => Tags.TryGetValue(key, out var value)
                                                    ? value
                                                    : null;

            public void Tag(string key, string value)
            {
                if (value == null)
                {
                    Tags.TryRemove(key, out value);
                }
                else
                {
                    Tags[key] = value;
                }
            }
        }
    }
}
