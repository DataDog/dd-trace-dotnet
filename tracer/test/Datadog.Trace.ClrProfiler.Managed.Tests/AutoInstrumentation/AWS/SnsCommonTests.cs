// <copyright file="SnsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    public class SnsCommonTests
    {
        private const string DatadogAttributeKey = "_datadog";

        private readonly SpanContext _spanContext;

        public SnsCommonTests()
        {
            ulong traceIdHigh = 1;
            ulong traceIdLow = 2;
            var traceId = new TraceId(traceIdHigh, traceIdLow);

            ulong spanId = 1;
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

            // Assert
            ((Dictionary<string, MessageAttributeValue>)proxy.MessageAttributes).Should().ContainKey(DatadogAttributeKey);
        }

        [Fact]
        public void InjectHeadersIntoMessage_MessageAttributesIsNull_CreatesMessageAttributes()
        {
            // Arrange
            var proxy = new Mock<IContainsMessageAttributes>();
            proxy.Setup(x => x.MessageAttributes).Returns((IDictionary)null);

            // Act
            ContextPropagation.InjectHeadersIntoMessage<PublishRequest>(proxy.Object, _spanContext);

            // Assert
            proxy.VerifySet(x => x.MessageAttributes = It.IsAny<IDictionary>(), Times.Once);
        }
    }
}
