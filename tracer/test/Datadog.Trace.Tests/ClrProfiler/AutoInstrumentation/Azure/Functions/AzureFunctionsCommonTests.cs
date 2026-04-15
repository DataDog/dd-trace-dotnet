// <copyright file="AzureFunctionsCommonTests.cs" company="Datadog">
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

#pragma warning disable SA1649 // File name should match first type name
namespace Microsoft.Azure.Functions.Worker.Context.Features
{
    internal interface IFunctionBindingsFeature
    {
    }
}
#pragma warning restore SA1649

#pragma warning disable SA1403 // File may only contain a single namespace
namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions
{
#pragma warning restore SA1403

    public class AzureFunctionsCommonTests
    {
        [Fact]
        public void ExtractPropagatedContextFromMessaging_MergesIntoEmptyBaggageCurrent()
        {
            var context = CreateMockFunctionContext(
                propertyKey: "Properties",
                headerProperties: new Dictionary<string, object>
                {
                    ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                    ["baggage"] = "user.id=123"
                });

            Baggage.Current = new Baggage();
            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromMessaging(
                context,
                "Properties",
                "PropertiesArray");

            extractedContext.MergeBaggageInto(Baggage.Current);

            extractedContext.SpanContext.Should().NotBeNull();
            extractedContext.Baggage.Should().NotBeNull();
            extractedContext.Baggage!["user.id"].Should().Be("123");

            Baggage.Current["user.id"].Should().Be("123");
        }

        [Fact]
        public void ExtractPropagatedContextFromMessaging_MergesIntoExistingBaggageCurrent()
        {
            var context = CreateMockFunctionContext(
                propertyKey: "Properties",
                headerProperties: new Dictionary<string, object>
                {
                    ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                    ["baggage"] = "user.id=123"
                });

            Baggage.Current = new Baggage
            {
                ["existing.key"] = "existing.value",
                ["user.id"] = "old.value"
            };

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromMessaging(
                context,
                "Properties",
                "PropertiesArray");

            extractedContext.MergeBaggageInto(Baggage.Current);

            extractedContext.SpanContext.Should().NotBeNull();
            extractedContext.Baggage.Should().NotBeNull();
            extractedContext.Baggage!["user.id"].Should().Be("123");

            Baggage.Current["existing.key"].Should().Be("existing.value");
            Baggage.Current["user.id"].Should().Be("123");
            Baggage.Current.Count.Should().Be(2);
        }

        [Fact]
        public void ExtractPropagatedContextFromEventGrid_SingleCloudEvent_ExtractsContext()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
                ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
                ["tracestate"] = "dd=s:1",
            };
            var context = CreateMockFunctionContextWithInputData("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromEventGrid(context, "myEvent");

            extractedContext.SpanContext.Should().NotBeNull();
        }

        [Fact]
        public void ExtractPropagatedContextFromEventGrid_SingleCloudEvent_TraceparentOnly()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
                ["traceparent"] = $"00-{1:x32}-{1:x16}-01",
            };
            var context = CreateMockFunctionContextWithInputData("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromEventGrid(context, "myEvent");

            extractedContext.SpanContext.Should().NotBeNull();
        }

        [Fact]
        public void ExtractPropagatedContextFromEventGrid_NoTraceContext_ReturnsDefault()
        {
            var cloudEvent = new Dictionary<string, object>
            {
                ["specversion"] = "1.0",
                ["type"] = "test.type",
                ["source"] = "/test/source",
                ["id"] = "test-id",
            };
            var context = CreateMockFunctionContextWithInputData("myEvent", JsonConvert.SerializeObject(cloudEvent));

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromEventGrid(context, "myEvent");

            extractedContext.SpanContext.Should().BeNull();
        }

        [Fact]
        public void ExtractPropagatedContextFromEventGrid_BatchCloudEvents_ReturnsDefault()
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
                },
                new Dictionary<string, object>
                {
                    ["specversion"] = "1.0",
                    ["type"] = "test.type",
                    ["source"] = "/test/source",
                    ["id"] = "test-id-2",
                    ["traceparent"] = $"00-{2:x32}-{2:x16}-01",
                },
            };
            var context = CreateMockFunctionContextWithInputData("myEvents", JsonConvert.SerializeObject(batch));

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromEventGrid(context, "myEvents");

            extractedContext.SpanContext.Should().BeNull();
        }

        [Fact]
        public void ExtractPropagatedContextFromEventGrid_NullBindingName_ReturnsDefault()
        {
            var context = CreateMockFunctionContextWithInputData("myEvent", "{}");

            var extractedContext = AzureFunctionsCommon.ExtractPropagatedContextFromEventGrid(context, null);

            extractedContext.SpanContext.Should().BeNull();
        }

        private static MockFunctionContext CreateMockFunctionContext(string propertyKey, Dictionary<string, object>? headerProperties)
        {
            var triggerMetadata = new Dictionary<string, object?>();

            if (headerProperties != null)
            {
                var json = JsonConvert.SerializeObject(headerProperties);
                triggerMetadata[propertyKey] = json;
            }

            var bindingsFeature = new MockBindingsFeature
            {
                TriggerMetadata = triggerMetadata
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

        private static MockFunctionContext CreateMockFunctionContextWithInputData(string bindingName, string inputDataJson)
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

        // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/IFunctionContext.cs
        private class MockFunctionContext : IFunctionContext
        {
            public FunctionDefinitionStruct FunctionDefinition { get; set; }

            public IEnumerable<KeyValuePair<Type, object?>>? Features { get; set; }
        }

        // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/GrpcBindingsFeatureStruct.cs
        private class MockBindingsFeature
        {
            public IDictionary<string, object?>? TriggerMetadata { get; set; }

            public IDictionary<string, object?>? InputData { get; set; }
        }
    }
}

#endif
