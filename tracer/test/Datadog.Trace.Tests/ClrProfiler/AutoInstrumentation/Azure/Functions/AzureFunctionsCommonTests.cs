// <copyright file="AzureFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections;
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

        [Theory]
        [InlineData("orchestrationTrigger", "DurableOrchestration")]
        [InlineData("activityTrigger", "DurableActivity")]
        [InlineData("entityTrigger", "DurableEntity")]
        [InlineData("httpTrigger", null)]
        public void DurableGetTriggerType_ClassifiesOnlyDurableInputs(string bindingType, string? expected)
        {
            var context = CreateMockFunctionContext(bindingType);

            AzureFunctionsDurableCommon.GetTriggerType(context).Should().Be(expected);
        }

        [Theory]
        [InlineData("00", null, null, null)]
        [InlineData("00", "vendor=value", null, "vendor=value")]
        [InlineData("00", "dd=s:0", 0, null)]
        [InlineData("00", "dd=s:-1", -1, null)]
        [InlineData("00", "dd=s:1;p:0000000000000002", 1, null)]
        [InlineData("01", null, 1, null)]
        public void DurableExtractPropagatedContext_UsesWorkerTraceContext(
            string traceFlags,
            string? traceState,
            int? expectedSamplingPriority,
            string? expectedAdditionalTraceState)
        {
            const string traceId = "00000000000000000000000000000001";
            const string spanId = "0000000000000002";
            var context = CreateMockFunctionContext("activityTrigger");
            context.TraceContext = new MockWorkerTraceContext
            {
                TraceParent = $"00-{traceId}-{spanId}-{traceFlags}",
                TraceState = traceState,
            };

            var extractedContext = AzureFunctionsDurableCommon.ExtractPropagatedContext(context);

            extractedContext.SpanContext.Should().NotBeNull();
            extractedContext.SpanContext!.RawTraceId.Should().Be(traceId);
            extractedContext.SpanContext.RawSpanId.Should().Be(spanId);
            extractedContext.SpanContext.SamplingPriority.Should().Be(expectedSamplingPriority);
            extractedContext.SpanContext.AdditionalW3CTraceState.Should().Be(expectedAdditionalTraceState);
        }

        [Theory]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-00", "dd=s:1;p:0000000000000001", "00-00000000000000000000000000000001-0000000000000001-01")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-02", "dd=s:2", "00-00000000000000000000000000000001-0000000000000001-03")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-01", "dd=s:1", "00-00000000000000000000000000000001-0000000000000001-01")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-00", "dd=s:0", "00-00000000000000000000000000000001-0000000000000001-00")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-00", null, "00-00000000000000000000000000000001-0000000000000001-00")]
        [InlineData("invalid", "dd=s:1", "invalid")]
        public void DurableReconcileTraceParentSampling_UsesPositiveDatadogDecision(string traceParent, string? traceState, string expected)
        {
            AzureFunctionsDurableCommon.ReconcileTraceParentSampling(traceParent, traceState).Should().Be(expected);
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

        private static MockFunctionContext CreateMockFunctionContext(string bindingType)
        {
            var inputBindings = new Hashtable
            {
                ["trigger"] = new MockBindingMetadata
                {
                    Direction = BindingDirection.In,
                    Type = bindingType,
                }
            };

            return new MockFunctionContext
            {
                FunctionDefinition = new FunctionDefinitionStruct
                {
                    InputBindings = inputBindings,
                    Name = "TestFunction",
                    EntryPoint = "Tests.TestFunction.Run",
                }
            };
        }

        // This duck types with the isolated-worker FunctionContext contracts.
        private class MockFunctionContext : IDurableFunctionContext
        {
            public FunctionDefinitionStruct FunctionDefinition { get; set; }

            public IEnumerable<KeyValuePair<Type, object?>>? Features { get; set; }

            public IDictionary<object, object?>? Items { get; }

            public object? TraceContext { get; set; }
        }

        private class MockBindingMetadata
        {
            public BindingDirection Direction { get; set; }

            public string? Type { get; set; }
        }

        private class MockWorkerTraceContext
        {
            public string? TraceParent { get; set; }

            public string? TraceState { get; set; }
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
