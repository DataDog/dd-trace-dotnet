// <copyright file="SpanContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanContextTests
    {
        [Fact]
        public void OverrideTraceIdWithoutParent()
        {
            const ulong expectedTraceId = 41;
            const ulong expectedSpanId = 42;

            var spanContext = new SpanContext(parent: null, traceContext: null, serviceName: "service", traceId: expectedTraceId, spanId: expectedSpanId);

            spanContext.SpanId.Should().Be(expectedSpanId);
            spanContext.TraceId.Should().Be(expectedTraceId);
        }

        [Fact]
        public void OverrideTraceIdWithParent()
        {
            const ulong parentTraceId = 41;
            const ulong parentSpanId = 42;

            const ulong childTraceId = 43;
            const ulong childSpanId = 44;

            var parent = new SpanContext(parentTraceId, parentSpanId);

            var spanContext = new SpanContext(parent: parent, traceContext: null, serviceName: "service", traceId: childTraceId, spanId: childSpanId);

            spanContext.SpanId.Should().Be(childSpanId);
            spanContext.TraceId.Should().Be(parentTraceId, "trace id shouldn't be overriden if a parent trace exists. Doing so would break the HttpWebRequest.GetRequestStream/GetResponse integration.");
        }

        [Fact]
        public void EnumerateKeys()
        {
            const ulong traceId = 1;
            const ulong spanId = 2;
            const int samplingPriority = SamplingPriorityValues.UserReject;
            const string origin = "origin";
            const string rawTraceId = "1a";
            const string rawSpanId = "2b";
            const string propagatedTags = "key1=value1;key2=value2";          // note: semicolon separator
            const string additionalW3CTraceState = "key3=value3,key4=value4"; // note: comma separator

            IReadOnlyDictionary<string, string> context = new SpanContext(
                                                              traceId,
                                                              spanId,
                                                              samplingPriority,
                                                              serviceName: null,
                                                              origin,
                                                              rawTraceId,
                                                              rawSpanId)
                                                          {
                                                              PropagatedTags = propagatedTags,
                                                              AdditionalW3CTraceState = additionalW3CTraceState
                                                          };

            context.Keys.Should().HaveCount(8);

            context.Keys.Should()
                   .Contain(
                        new[]
                        {
                            "__DistributedKey-TraceId",
                            "__DistributedKey-ParentId",
                            "__DistributedKey-SamplingPriority",
                            "__DistributedKey-Origin",
                            "__DistributedKey-RawTraceId",
                            "__DistributedKey-RawSpanId",
                            "__DistributedKey-PropagatedTags",
                            "__DistributedKey-AdditionalW3CTraceState",

                            "x-datadog-trace-id",
                            "x-datadog-parent-id",
                            "x-datadog-sampling-priority",
                            "x-datadog-origin",
                        });
        }
    }
}
