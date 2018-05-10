using System;
using System.Collections.Generic;
using Datadog.Trace.Propagators;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class HeaderCollectionPropagatorTests
    {
        // The values are duplicated here to make sure that if they are changed it will break tests
        private const string HttpHeaderTraceId = "x-datadog-trace-id";
        private const string HttpHeaderParentId = "x-datadog-parent-id";

        private IHeaderCollection _headers;

        public HeaderCollectionPropagatorTests()
        {
            var headersMock = new Mock<IHeaderCollection>();
            var headersDict = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            headersMock.Setup(x => x.Get(It.IsAny<string>())).Returns((string x) =>
            {
                headersDict.TryGetValue(x, out var value);
                return value;
            });
            headersMock.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string k, string v) =>
            {
                headersDict.Add(k, v);
            });
            _headers = headersMock.Object;
        }

        [Fact]
        public void Extract_NoHeaders_Exception()
        {
            var context = HeaderCollectionPropagator.Extract(_headers);
            Assert.Null(context);
        }

        [Fact]
        public void Extract_MissingTraceIdHeader_Exception()
        {
            _headers.Set(HttpHeaderParentId, "10");
            var context = HeaderCollectionPropagator.Extract(_headers);
            Assert.Null(context);
        }

        [Fact]
        public void Extract_MissingParentIdHeader_Exception()
        {
            _headers.Set(HttpHeaderTraceId, "10");
            var context = HeaderCollectionPropagator.Extract(_headers);
            Assert.Null(context);
        }

        [Fact]
        public void Extract_NonIntegerTraceId_Exception()
        {
            _headers.Set(HttpHeaderTraceId, "hello");
            _headers.Set(HttpHeaderParentId, "10");
            var context = HeaderCollectionPropagator.Extract(_headers);
            Assert.Null(context);
        }

        [Fact]
        public void Extract_NonIntegerParentId_Exception()
        {
            _headers.Set(HttpHeaderTraceId, "hello");
            _headers.Set(HttpHeaderParentId, "10");
            var context = HeaderCollectionPropagator.Extract(_headers);
            Assert.Null(context);
        }

        [Fact]
        public void Extract_ValidParentAndTraceId_ProperSpanContext()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;
            _headers.Set(HttpHeaderTraceId, traceId.ToString());
            _headers.Set(HttpHeaderParentId, parentId.ToString());
            var spanContext = HeaderCollectionPropagator.Extract(_headers);

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.TraceId);
            Assert.Equal(parentId, spanContext.SpanId);
        }

        [Fact]
        public void Extract_WrongHeaderCase_ExtractionStillWorks()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;
            _headers.Set(HttpHeaderTraceId.ToUpper(), traceId.ToString());
            _headers.Set(HttpHeaderParentId.ToUpper(), parentId.ToString());
            var spanContext = HeaderCollectionPropagator.Extract(_headers);

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.TraceId);
            Assert.Equal(parentId, spanContext.SpanId);
        }

        [Fact]
        public void Inject_SpanContext_HeadersWithCorrectInfo()
        {
            const ulong spanId = 10;
            const ulong traceId = 7;
            var spanContext = new SpanContext(traceId, spanId);

            HeaderCollectionPropagator.Inject(_headers, spanContext);

            Assert.Equal(spanId.ToString(), _headers.Get(HttpHeaderParentId));
            Assert.Equal(traceId.ToString(), _headers.Get(HttpHeaderTraceId));
        }
    }
}
