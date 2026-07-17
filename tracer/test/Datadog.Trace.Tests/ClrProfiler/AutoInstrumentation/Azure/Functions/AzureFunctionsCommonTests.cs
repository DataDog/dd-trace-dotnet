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

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions
{
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
    }
}

#endif
