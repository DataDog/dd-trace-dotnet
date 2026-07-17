// <copyright file="EventGridFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    public class EventGridFunctionsCommonTests
    {
        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_SingleCloudEvent_ExtractsContext()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
                ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                ["tracestate"] = "dd=s:1",
                ["baggage"] = "user.id=123",
            };
            var context = CreateMockFunctionContext("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContexts = ExtractPropagatedContexts(context, "myEvent");

            extractedContexts.Should().ContainSingle();
            extractedContexts[0].SpanContext.Should().NotBeNull();
            extractedContexts[0].Baggage.Should().NotBeNull();
            extractedContexts[0].Baggage!["user.id"].Should().Be("123");
        }

        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_SingleCloudEvent_TraceparentOnly()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
                ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
            };
            var context = CreateMockFunctionContext("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContexts = ExtractPropagatedContexts(context, "myEvent");

            extractedContexts.Should().ContainSingle();
            extractedContexts[0].SpanContext.Should().NotBeNull();
        }

        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_NoTraceContext_ReturnsEmpty()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
            };
            var context = CreateMockFunctionContext("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContexts = ExtractPropagatedContexts(context, "myEvent");

            extractedContexts.Should().BeEmpty();
        }

        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_BatchCloudEvents_ExtractsAllContextsAndMergesFirstBaggage()
        {
            var batch = new[]
            {
                new Dictionary<string, object>
                {
                    ["specversion"] = "1.0",
                    ["type"] = "test.type",
                    ["source"] = "/test/source",
                    ["id"] = "test-id-1",
                    ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                    ["baggage"] = "tenant=first,first.only=true",
                },
                new Dictionary<string, object>
                {
                    ["specversion"] = "1.0",
                    ["type"] = "test.type",
                    ["source"] = "/test/source",
                    ["id"] = "test-id-2",
                    ["traceparent"] = $"00-{2:x32}-{2:x16}-01",
                    ["baggage"] = "tenant=second,second.only=true",
                },
            };
            var context = CreateMockFunctionContext("myEvents", JsonConvert.SerializeObject(batch));

            var extractedContexts = ExtractPropagatedContexts(context, "myEvents");
            var spanLinks = EventGridFunctionsCommon.CreateSpanLinks(extractedContexts);
            var destinationBaggage = new Baggage { ["existing"] = "value" };
            EventGridFunctionsCommon.MergeBaggageFromFirstContext(extractedContexts, destinationBaggage);

            extractedContexts.Should().HaveCount(2);
            extractedContexts[0].SpanContext!.SpanId.Should().Be(1);
            extractedContexts[1].SpanContext!.SpanId.Should().Be(2);
            destinationBaggage["existing"].Should().Be("value");
            destinationBaggage["tenant"].Should().Be("first");
            destinationBaggage["first.only"].Should().Be("true");
            destinationBaggage.TryGetValue("second.only", out _).Should().BeFalse();
            spanLinks.Should().HaveCount(2);
            spanLinks![0].Context.SpanId.Should().Be(1);
            spanLinks[1].Context.SpanId.Should().Be(2);
        }

        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_BatchCloudEvents_DeduplicatesContextsAndSkipsMissingContexts()
        {
            var batch = new[]
            {
                new Dictionary<string, object>
                {
                    ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                },
                new Dictionary<string, object>
                {
                    ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                },
                new Dictionary<string, object>
                {
                    ["specversion"] = "1.0",
                },
            };
            var context = CreateMockFunctionContext("myEvents", JsonConvert.SerializeObject(batch));

            var extractedContexts = ExtractPropagatedContexts(context, "myEvents");

            extractedContexts.Should().ContainSingle();
            extractedContexts[0].SpanContext!.SpanId.Should().Be(1);
        }

        [Fact]
        public void ExtractPropagatedContextsFromEventGrid_NullBindingName_ReturnsEmpty()
        {
            var context = CreateMockFunctionContext("myEvent", "{}");

            var extractedContexts = ExtractPropagatedContexts(context, null);

            extractedContexts.Should().BeEmpty();
        }

        private static List<PropagationContext> ExtractPropagatedContexts(MockFunctionContext context, string? bindingName)
        {
            var cloudEvents = EventGridFunctionsCommon.GetCloudEvents(context, bindingName);
            return EventGridFunctionsCommon.ExtractPropagatedContexts(cloudEvents);
        }

        private static MockFunctionContext CreateMockFunctionContext(string bindingName, string inputDataJson)
        {
            var inputData = new Dictionary<string, object?>
            {
                [bindingName] = inputDataJson
            };

            var bindingsFeature = new MockBindingsFeature
            {
                InputData = inputData
            };

            var features = new List<KeyValuePair<Type, object?>>
            {
                new(typeof(Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature), bindingsFeature)
            };

            return new MockFunctionContext
            {
                Features = features
            };
        }
    }
}

#endif
