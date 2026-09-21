// <copyright file="AzureFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
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

    [Collection(nameof(Datadog.Trace.Tests.TracerInstanceTestCollection))]
    [TracerRestorer]
    public class AzureFunctionsCommonTests
    {
        [Fact]
        public async System.Threading.Tasks.Task OnIsolatedFunctionBegin_DoesNotOverwriteInferredProxyRootSpan()
        {
            // Regression test for the guard in CreateIsolatedFunctionScope: when an inferred proxy
            // span (e.g. azure.frontdoor) is the trace root and the function span is created as a
            // *child* of it, the function tags/type/resource must NOT be copied onto the proxy root.
            var settings = new TracerSettings();
            await using var scopedTracer = TracerHelper.CreateWithFakeAgent(settings);
            TracerRestorerAttribute.SetTracer(scopedTracer);

            // Simulate the inferred proxy span as the active trace root.
            using var proxyScope = scopedTracer.StartActiveInternal("azure.frontdoor");
            proxyScope.Span.Type = SpanTypes.Web;
            proxyScope.Span.ResourceName = "GET /api/test";

            // A minimal isolated-function context: no ASP.NET Core bridge (Items is null) and no
            // input bindings, so the function span is parented to the active (proxy) scope and the
            // "not the local root" branch is exercised.
            var context = new MockFunctionContext
            {
                FunctionDefinition = new FunctionDefinitionStruct
                {
                    Name = "MyFunction",
                    EntryPoint = "MyNamespace.MyFunction",
                    InputBindings = new Hashtable(),
                },
            };

            var state = AzureFunctionsCommon.OnIsolatedFunctionBegin(context);

            // A child function span was created, rooted at the proxy span.
            state.Scope.Should().NotBeNull();
            var functionSpan = state.Scope!.Span;
            functionSpan.Should().NotBeSameAs(proxyScope.Span);
            state.Scope.Root.Span.Should().BeSameAs(proxyScope.Span);

            // The proxy root span must be untouched by the function instrumentation.
            proxyScope.Span.OperationName.Should().Be("azure.frontdoor");
            proxyScope.Span.Type.Should().Be(SpanTypes.Web);
            proxyScope.Span.ResourceName.Should().Be("GET /api/test");
        }

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

        // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/IFunctionContext.cs
        private class MockFunctionContext : IFunctionContext
        {
            public FunctionDefinitionStruct FunctionDefinition { get; set; }

            public IEnumerable<KeyValuePair<Type, object?>>? Features { get; set; }

            public IDictionary<object, object?>? Items { get; }
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
