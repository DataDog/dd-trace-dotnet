// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    public class ContextPropagationTests
    {
        [Fact]
        public void Inject_Uses_String_Header_Values_For_W3C()
        {
            var tracer = new StubDatadogTracer();
            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, mechanism: null, notifyDistributedTracer: false);

            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)123456789UL, spanId: 0x3ade68b1);
            var context = new PropagationContext(spanContext, baggage: null);

            var carrier = new Dictionary<string, object>();
            W3CTraceContextPropagator.Instance.Inject(context, carrier, default(ContextPropagation));

            carrier.Should().ContainKey(W3CTraceContextPropagator.TraceParentHeaderName);
            carrier.Should().ContainKey(W3CTraceContextPropagator.TraceStateHeaderName);

            carrier[W3CTraceContextPropagator.TraceParentHeaderName].Should().BeOfType<string>();
            carrier[W3CTraceContextPropagator.TraceStateHeaderName].Should().BeOfType<string>();

            var expectedTraceparent = W3CTraceContextPropagator.CreateTraceParentHeader(spanContext);
            var expectedTracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            carrier[W3CTraceContextPropagator.TraceParentHeaderName].Should().Be(expectedTraceparent);
            carrier[W3CTraceContextPropagator.TraceStateHeaderName].Should().Be(expectedTracestate);
        }

        [Fact]
        public void Get_Returns_Value_When_Header_Is_String()
        {
            var expected = "00-000000000000000000000000075bcd15-000000003ade68b1-01";
            var carrier = new Dictionary<string, object>
            {
                { W3CTraceContextPropagator.TraceParentHeaderName, expected }
            };

            var values = default(ContextPropagation).Get(carrier, W3CTraceContextPropagator.TraceParentHeaderName);
            values.Should().ContainSingle().Which.Should().Be(expected);
        }

        [Fact]
        public void Get_Returns_Value_When_Header_Is_Bytes()
        {
            var expected = "00-000000000000000000000000075bcd15-000000003ade68b1-01";
            var carrier = new Dictionary<string, object>
            {
                { W3CTraceContextPropagator.TraceParentHeaderName, Encoding.UTF8.GetBytes(expected) }
            };

            var values = default(ContextPropagation).Get(carrier, W3CTraceContextPropagator.TraceParentHeaderName);
            values.Should().ContainSingle().Which.Should().Be(expected);
        }
    }
}
