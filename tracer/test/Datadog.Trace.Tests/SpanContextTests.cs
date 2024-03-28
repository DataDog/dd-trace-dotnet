// <copyright file="SpanContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanContextTests
    {
        [Fact]
        public void OverrideTraceIdWithoutParent()
        {
            var expectedTraceId = (TraceId)41;
            const ulong expectedSpanId = 42;

            var spanContext = new SpanContext(parent: null, traceContext: null, serviceName: "service", traceId: expectedTraceId, spanId: expectedSpanId);

            spanContext.SpanId.Should().Be(expectedSpanId);
            spanContext.TraceId128.Should().Be(expectedTraceId);
            spanContext.TraceId.Should().Be(expectedTraceId.Lower);
        }

        [Fact]
        public void OverrideTraceIdWithParent()
        {
            var parentTraceId = (TraceId)41;
            const ulong parentSpanId = 42;

            var childTraceId = (TraceId)43;
            const ulong childSpanId = 44;

            var parent = new SpanContext(parentTraceId, parentSpanId, samplingPriority: null, serviceName: null, origin: null);

            var spanContext = new SpanContext(parent: parent, traceContext: null, serviceName: "service", traceId: childTraceId, spanId: childSpanId);

            spanContext.SpanId.Should().Be(childSpanId);
            spanContext.TraceId128.Should().Be(parentTraceId, "trace id shouldn't be overriden if a parent trace exists. Doing so would break the HttpWebRequest.GetRequestStream/GetResponse integration.");
            spanContext.TraceId.Should().Be(parentTraceId.Lower);
        }

        [Fact]
        public void EnumerateKeys()
        {
            var expectedKeys = new HashSet<string>
                               {
                                   "__DistributedKey-TraceId",
                                   "__DistributedKey-ParentId",
                                   "__DistributedKey-SamplingPriority",
                                   "__DistributedKey-Origin",
                                   "__DistributedKey-RawTraceId",
                                   "__DistributedKey-RawSpanId",
                                   "__DistributedKey-PropagatedTags",
                                   "__DistributedKey-AdditionalW3CTraceState",
                                   "__DistributedKey-LastParentId",
                                   "x-datadog-trace-id",
                                   "x-datadog-parent-id",
                                   "x-datadog-sampling-priority",
                                   "x-datadog-origin",
                               };

            var context = CreateSpanContext();
            var actualKeys = context.Keys.ToArray();

            // check for missing and unexpected keys
            actualKeys.Should().BeSubsetOf(expectedKeys);
            expectedKeys.Should().BeSubsetOf(actualKeys);

            // check for duplicate keys
            actualKeys.Should().BeEquivalentTo(actualKeys.Distinct());
        }

        [Fact]
        public void EnumerateValues()
        {
            var expectedValues = new HashSet<string>
                                 {
                                     "1",
                                     "2",
                                     "-1",
                                     "origin",
                                     "1a",
                                     "2b",
                                     "_dd.p.key1=value1,_dd.p.key2=value2",
                                     "key3=value3,key4=value4",
                                     "0123456789abcdef",
                                 };

            var context = CreateSpanContext();
            var actualValues = context.Values.ToArray();

            // check for missing and unexpected values
            actualValues.Should().BeSubsetOf(expectedValues);
            expectedValues.Should().BeSubsetOf(actualValues);
        }

        [Fact]
        public void EnumeratePairs()
        {
            var expectedPairs = new HashSet<KeyValuePair<string, string>>
                                {
                                    new("__DistributedKey-TraceId", "1"),
                                    new("__DistributedKey-ParentId", "2"),
                                    new("__DistributedKey-SamplingPriority", "-1"),
                                    new("__DistributedKey-Origin", "origin"),
                                    new("__DistributedKey-RawTraceId", "1a"),
                                    new("__DistributedKey-RawSpanId", "2b"),
                                    new("__DistributedKey-PropagatedTags", "_dd.p.key1=value1,_dd.p.key2=value2"),
                                    new("__DistributedKey-AdditionalW3CTraceState", "key3=value3,key4=value4"),
                                    new("__DistributedKey-LastParentId", "0123456789abcdef"),

                                    new("x-datadog-trace-id", "1"),
                                    new("x-datadog-parent-id", "2"),
                                    new("x-datadog-sampling-priority", "-1"),
                                    new("x-datadog-origin", "origin")
                                };

            var context = CreateSpanContext();
            var actualPairs = context.ToArray();

            // check for missing and unexpected keys
            actualPairs.Should().BeSubsetOf(expectedPairs);
            expectedPairs.Should().BeSubsetOf(actualPairs);

            // check for duplicate keys
            actualPairs.Should().BeEquivalentTo(actualPairs.Distinct());
        }

        private static IReadOnlyDictionary<string, string> CreateSpanContext()
        {
            var traceId = (TraceId)1;
            const ulong spanId = 2;
            const string rawTraceId = "1a";
            const string rawSpanId = "2b";
            const int samplingPriority = SamplingPriorityValues.UserReject;
            const string origin = "origin";
            const string additionalW3CTraceState = "key3=value3,key4=value4";
            const string lastParentId = "0123456789abcdef";

            var propagatedTags = new TraceTagCollection();
            propagatedTags.SetTag("_dd.p.key1", "value1");
            propagatedTags.SetTag("_dd.p.key2", "value2");

            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>(), propagatedTags);
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
                rawSpanId)
            {
                LastParentId = lastParentId
            };
        }
    }
}
