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
using MockBindingsFeature = Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions.AzureFunctionsCommonTests.MockBindingsFeature;
using MockFunctionContext = Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions.AzureFunctionsCommonTests.MockFunctionContext;

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
