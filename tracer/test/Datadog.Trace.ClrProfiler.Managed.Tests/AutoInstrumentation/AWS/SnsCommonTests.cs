// <copyright file="SnsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    public class SnsCommonTests
    {
        private const string DatadogAttributeKey = "_datadog";

        private readonly SpanContext _spanContext;
        private readonly TraceId _traceId;
        private readonly string _parentId;

        public SnsCommonTests()
        {
            ulong upper = 1234567890123456789;
            ulong lower = 9876543210987654321;

            var newTraceId = new TraceId(upper, lower);
            ulong spanId = 6766950223540265769;
            _spanContext = new SpanContext(newTraceId, spanId, 0, "test-service", "origin");
            _traceId = newTraceId;
            _parentId = spanId.ToString();
        }

        [Fact]
        public void InjectHeadersIntoMessage_AddsDatadogAttribute()
        {
            // Arrange
            var request = new PublishRequest();
            var proxy = new PublishRequestProxy(request);

            // Act
            ContextPropagation.InjectHeadersIntoMessage<PublishRequest>(proxy, _spanContext);

            // Assert
            var attributes = (Dictionary<string, MessageAttributeValue>)proxy.MessageAttributes;

            attributes.Should().ContainKey(DatadogAttributeKey);
            if (attributes.TryGetValue(DatadogAttributeKey, out var attributeValue))
            {
                var ddTraceContextMemoryStream = attributeValue.BinaryValue;
                ddTraceContextMemoryStream.Position = 0; // Reset the position, in case it's at the end
                var reader = new StreamReader(ddTraceContextMemoryStream);
                var jsonString = reader.ReadToEnd();

                var traceContextJson = JObject.Parse(jsonString);
                var traceId = traceContextJson["x-datadog-trace-id"].Value<string>();
                var parentId = traceContextJson["x-datadog-parent-id"].Value<string>();
                Assert.Equal(_parentId, parentId);
                Assert.Equal("9876543210987654321", traceId);
            }
            else
            {
                throw new Exception("DatadogAttributeKey not found in MessageAttributes.");
            }
        }
    }
}
