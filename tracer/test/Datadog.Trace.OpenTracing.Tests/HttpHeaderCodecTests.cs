// <copyright file="HttpHeaderCodecTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class HttpHeaderCodecTests
    {
        // The values are duplicated here to make sure that if they are changed it will break tests
        private const string HttpHeaderTraceId = "x-datadog-trace-id";
        private const string HttpHeaderParentId = "x-datadog-parent-id";
        private const string HttpHeaderSamplingPriority = "x-datadog-sampling-priority";

        private readonly HttpHeadersCodec _codec = new HttpHeadersCodec();

        [Fact]
        public void Extract_ValidParentAndTraceId_ProperSpanContext()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;

            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId, traceId.ToString());
            headers.Set(HttpHeaderParentId, parentId.ToString());

            var spanContext = _codec.Extract(headers) as OpenTracingSpanContext;

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.Context.TraceId);
            Assert.Equal(parentId, spanContext.Context.SpanId);
        }

        [Fact]
        public void Extract_WrongHeaderCase_ExtractionStillWorks()
        {
            const ulong traceId = 10;
            const ulong parentId = 120;
            const int samplingPriority = SamplingPriorityInternal.UserKeep;

            var headers = new MockTextMap();
            headers.Set(HttpHeaderTraceId.ToUpper(), traceId.ToString());
            headers.Set(HttpHeaderParentId.ToUpper(), parentId.ToString());
            headers.Set(HttpHeaderSamplingPriority.ToUpper(), samplingPriority.ToString());

            var spanContext = _codec.Extract(headers) as OpenTracingSpanContext;

            Assert.NotNull(spanContext);
            Assert.Equal(traceId, spanContext.Context.TraceId);
            Assert.Equal(parentId, spanContext.Context.SpanId);
        }

        [Fact]
        public void Inject_SpanContext_HeadersWithCorrectInfo()
        {
            const ulong spanId = 10;
            const ulong traceId = 7;
            const int samplingPriority = SamplingPriorityInternal.UserKeep;

            var ddSpanContext = new SpanContext(traceId, spanId, (SamplingPriority)samplingPriority);
            var spanContext = new OpenTracingSpanContext(ddSpanContext);
            var headers = new MockTextMap();

            _codec.Inject(spanContext, headers);

            Assert.Equal(spanId.ToString(), headers.Get(HttpHeaderParentId));
            Assert.Equal(traceId.ToString(), headers.Get(HttpHeaderTraceId));
            Assert.Equal(samplingPriority.ToString(), headers.Get(HttpHeaderSamplingPriority));
        }
    }
}
