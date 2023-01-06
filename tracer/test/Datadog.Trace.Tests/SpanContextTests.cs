// <copyright file="SpanContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Tagging;
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
            var context = CreateSpanContext();

            // fail test if new keys are added
            context.Keys.Should().HaveCount(12);

            // fail tests if any key is missing
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

        [Fact]
        public void EnumerateValues()
        {
            var context = CreateSpanContext();

            context.Values.Should().HaveCount(12);

            context.Values.Should()
                   .Contain(
                        new[]
                        {
                            "1",      // twice
                            "2",      // twice
                            "-1",     // twice
                            "origin", // twice
                            "1a",
                            "2b",
                            "_dd.p.key1=value1,_dd.p.key2=value2",
                            "key3=value3,key4=value4",
                        });
        }

        [Fact]
        public void EnumeratePairs()
        {
            var context = CreateSpanContext();

            // fail test if new keys are added
            context.Should().HaveCount(12);

            // fail tests if any key is missing
            context.Should()
                   .Contain(
                        new("__DistributedKey-TraceId", "1"),
                        new("__DistributedKey-ParentId", "2"),
                        new("__DistributedKey-SamplingPriority", "-1"),
                        new("__DistributedKey-Origin", "origin"),
                        new("__DistributedKey-RawTraceId", "1a"),
                        new("__DistributedKey-RawSpanId", "2b"),
                        new("__DistributedKey-PropagatedTags", "_dd.p.key1=value1,_dd.p.key2=value2"),
                        new("__DistributedKey-AdditionalW3CTraceState", "key3=value3,key4=value4"),
                        new("x-datadog-trace-id", "1"),
                        new("x-datadog-parent-id", "2"),
                        new("x-datadog-sampling-priority", "-1"),
                        new("x-datadog-origin", "origin"));
        }

        private static IReadOnlyDictionary<string, string> CreateSpanContext()
        {
            const ulong traceId = 1;
            const ulong spanId = 2;
            const string rawTraceId = "1a";
            const string rawSpanId = "2b";
            const int samplingPriority = SamplingPriorityValues.UserReject;
            const string origin = "origin";
            const string additionalW3CTraceState = "key3=value3,key4=value4";

            var propagatedTags = new TraceTagCollection(100);
            propagatedTags.SetTag("_dd.p.key1", "value1");
            propagatedTags.SetTag("_dd.p.key2", "value2");

            var traceContext = new TraceContext(tracer: null, propagatedTags);
            traceContext.SetSamplingPriority(samplingPriority);
            traceContext.Origin = origin;
            traceContext.AdditionalW3CTraceState = additionalW3CTraceState;

            return new SpanContext(
                parent: SpanContext.None,
                traceContext,
                serviceName: null,
                traceId,
                spanId,
                rawTraceId,
                rawSpanId);
        }
    }
}
