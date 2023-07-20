// <copyright file="SnsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    public class SnsCommonTests
    {
        private const string DatadogAttributeKey = "_datadog";

        private readonly SpanContext _spanContext;

        public SnsCommonTests()
        {
            ulong upper = 1234567890123456789;
            ulong lower = 9876543210987654321;

            var traceId = new TraceId(upper, lower);
            ulong spanId = 6766950223540265769;
            _spanContext = new SpanContext(traceId, spanId, 0, "test-service", "origin");
        }

        [Fact]
        public void InjectHeadersIntoMessage_AddsDatadogAttribute()
        {
            // Arrange
            var request = new PublishRequest();
            var proxy = new PublishRequestProxy(request);

            // Act
            ContextPropagation.InjectHeadersIntoMessage<PublishRequest>(proxy, _spanContext);

            // Now, attempt to extract the SpanContext from the message
            var getter = new MemoryStreamCarrierGetter();
            var extracted = DatadogContextPropagator.Instance.TryExtract(proxy, getter, out var extractedSpanContext);

            // Assert
            extracted.Should().BeTrue();
            extractedSpanContext.Should().NotBeNull();
            extractedSpanContext.TraceId.Should().Be(_spanContext.TraceId);
            extractedSpanContext.SpanId.Should().Be(_spanContext.SpanId);
            proxy.CloseMemoryStream();
        }
    }
}
