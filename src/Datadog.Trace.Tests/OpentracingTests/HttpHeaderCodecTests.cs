using System;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class HttpHeaderCodecTests
    {
        // The values are duplicated here to make sure that if they are changed it will break tests
        public const string HttpHeaderTraceId = "x-datadog-trace-id";
        public const string HttpHeaderParentId = "x-datadog-parent-id";

        private HttpHeadersCodec _codec;
        private Mock<IDatadogTracer> _tracerMock;

        public HttpHeaderCodecTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            _tracerMock.Setup(x => x.DefaultServiceName).Returns("Plop");
            _codec = new HttpHeadersCodec(_tracerMock.Object);
        }

        [Fact]
        public void Extract_NoHeaders_Exception()
        {
            Assert.Throws<ArgumentException>(() => _codec.Extract(new MockTextMap()));
        }

        [Fact]
        public void Extract_MissingTraceIdHeader_Exception()
        {
            var headers = new MockTextMap();
            headers.Set(HttpHeaderParentId, "10");
            Assert.Throws<ArgumentException>(() => _codec.Extract(headers));
        }

        [Fact]
        public void Extract_MissingParentIdHeader_Exception()
        {
            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId, "10");
            Assert.Throws<ArgumentException>(() => _codec.Extract(headers));
        }

        [Fact]
        public void Extract_NonIntegerTraceId_Exception()
        {
            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId, "hello");
            headers.Set(HttpHeaderParentId, "10");
            Assert.Throws<FormatException>(() => _codec.Extract(headers));
        }

        [Fact]
        public void Extract_NonIntegerParentId_Exception()
        {
            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId, "hello");
            headers.Set(HttpHeaderParentId, "10");
            Assert.Throws<FormatException>(() => _codec.Extract(headers));
        }

        [Fact]
        public void Extract_ValidParentAndTraceId_ProperSpanContext()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId, traceId.ToString());
            headers.Set(HttpHeaderParentId, parentId.ToString());
            var spanContext = _codec.Extract(headers);

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.TraceId);
            Assert.Equal(parentId, spanContext.SpanId);
        }

        [Fact]
        public void Extract_WrongHeaderCase_ExtractionStillWorks()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId.ToUpper(), traceId.ToString());
            headers.Set(HttpHeaderParentId.ToUpper(), parentId.ToString());
            var spanContext = _codec.Extract(headers);

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.TraceId);
            Assert.Equal(parentId, spanContext.SpanId);
        }

        [Fact]
        public void Inject_SpanContext_HeadersWithCorrectInfo()
        {
            var spanContext = new SpanContext(_tracerMock.Object, null, "MyService");
            var headers = new MockTextMap();

            _codec.Inject(spanContext, headers);

            Assert.Equal(spanContext.SpanId.ToString(), headers.Get(HttpHeaderParentId));
            Assert.Equal(spanContext.TraceId.ToString(), headers.Get(HttpHeaderTraceId));
        }
    }
}
